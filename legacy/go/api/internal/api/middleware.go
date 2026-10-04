package api

import (
	"context"
	"net/http"
	"strconv"
	"strings"

	"lodb/go/api/internal/keys"
	"lodb/go/api/internal/quota"
	"lodb/go/api/internal/ratelimit"
)

type entryContextKey struct{}

// entryFromContext returns the authenticated key entry stashed by withAuth.
func entryFromContext(ctx context.Context) *keys.Entry {
	entry, _ := ctx.Value(entryContextKey{}).(*keys.Entry)
	return entry
}

type callerContextKey struct{}

// callerInfo is the one mutable cell the access log reads back after the
// handler chain has run: authentication happens on a DERIVED request the
// outermost middleware cannot observe. Written once, in the same goroutine that
// reads it, so no synchronisation is needed.
type callerInfo struct{ apiKeyID int }

func withCaller(ctx context.Context, caller *callerInfo) context.Context {
	return context.WithValue(ctx, callerContextKey{}, caller)
}

// nameCaller records WHICH key served the request, for the access line. The key
// itself never travels — an id joins back to api_keys where it is legally held.
func nameCaller(ctx context.Context, entry *keys.Entry) {
	caller, ok := ctx.Value(callerContextKey{}).(*callerInfo)
	if ok && entry != nil {
		caller.apiKeyID = entry.KeyID()
	}
}

// The verdicts of the authentication/billing pipeline: each step decides,
// withAuth renders — which is what makes the pipeline testable without a
// ResponseWriter. Treated as immutable: never reassign a field.
var (
	failMissingKey = &apiError{
		http.StatusUnauthorized, CodeUnauthorized,
		"missing API key: use Authorization: Bearer <key> or X-Api-Key",
	}
	failMalformedKey = &apiError{
		http.StatusUnauthorized, CodeUnauthorized, "malformed API key",
	}
	failUnknownKey = &apiError{
		http.StatusUnauthorized, CodeUnauthorized, "unknown API key",
	}
	failRevokedKey = &apiError{
		http.StatusForbidden, CodeForbidden, "API key is revoked or inactive",
	}
	failRateLimited = &apiError{
		http.StatusTooManyRequests, CodeRateLimited,
		"rate limit exceeded, retry after X-RateLimit-Reset",
	}
	failQuotaExceeded = &apiError{
		http.StatusTooManyRequests, CodeQuotaExceeded,
		"monthly quota exhausted and no credits left",
	}
	// failDependencyDown mirrors writeUnavailable: same envelope, same 503.
	failDependencyDown = &apiError{
		http.StatusServiceUnavailable, CodeInternal, "service temporarily unavailable",
	}
)

// withAuth authenticates and rate-limits a /v1 handler. enforceQuota is a
// documented orthogonal opt-out: /v1/usage stays reachable (and free) when the
// quota is exhausted so clients can always inspect their consumption.
func (s *Server) withAuth(enforceQuota bool, next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		entry, fail := s.resolveKey(r.Context(), rawKeyFromRequest(r))
		if fail == nil {
			fail = s.applyRateLimit(w, entry)
		}
		if fail == nil && enforceQuota {
			fail = s.applyQuota(r.Context(), entry)
		}
		nameCaller(r.Context(), entry)
		if fail != nil {
			fail.write(w)
			return
		}
		next(w, r.WithContext(context.WithValue(r.Context(), entryContextKey{}, entry)))
	}
}

// resolveKey validates the raw key and resolves it through the cache.
func (s *Server) resolveKey(ctx context.Context, raw string) (*keys.Entry, *apiError) {
	if raw == "" {
		return nil, failMissingKey
	}
	hash, err := keys.Hash(raw)
	if err != nil {
		return nil, failMalformedKey
	}
	entry := s.keyCache.Get(hash)
	if entry == nil {
		loaded, fail := s.loadKey(ctx, hash)
		if fail != nil {
			return nil, fail
		}
		entry = loaded
	}
	if entry.Invalid {
		return nil, failUnknownKey
	}
	if !entry.Usable() {
		return nil, failRevokedKey
	}
	return entry, nil
}

// loadKey fills the cache from the database (positive or negative entry).
func (s *Server) loadKey(ctx context.Context, hash string) (*keys.Entry, *apiError) {
	key, err := s.auth.KeyByHash(ctx, hash)
	if isNotFound(err) {
		s.keyCache.PutInvalid(hash)
		return nil, failUnknownKey
	}
	if err != nil {
		s.log.Error("api key lookup failed", "error", err)
		return nil, failDependencyDown
	}
	if !key.Usable() {
		// Cache the row itself (not a negative entry) so the verdict stays 403.
		return s.keyCache.PutValid(hash, key, 0), nil
	}
	used, err := s.auth.MonthlyUsage(ctx, key.ID)
	if err != nil {
		s.log.Error("monthly usage lookup failed", "error", err, "api_key_id", key.ID)
		return nil, failDependencyDown
	}
	return s.keyCache.PutValid(hash, key, used), nil
}

// applyRateLimit consumes a token. It touches the writer only to advertise the
// X-RateLimit-* state, which belongs to allowed responses as much as to refusals.
func (s *Server) applyRateLimit(w http.ResponseWriter, entry *keys.Entry) *apiError {
	verdict := s.limiter.Allow(entry.KeyID(), entry.RateLimitPerMin())
	setRateLimitHeaders(w.Header(), verdict)
	if !verdict.Allowed {
		return failRateLimited
	}
	return nil
}

func setRateLimitHeaders(header http.Header, verdict ratelimit.Verdict) {
	header.Set("X-RateLimit-Limit", strconv.Itoa(verdict.Limit))
	header.Set("X-RateLimit-Remaining", strconv.Itoa(verdict.Remaining))
	header.Set("X-RateLimit-Reset", strconv.FormatInt(verdict.Reset, 10))
}

// applyQuota charges the request to the monthly plan, then prepaid credits.
func (s *Server) applyQuota(ctx context.Context, entry *keys.Entry) *apiError {
	switch quota.Evaluate(entry.MonthlyUsed(), entry.MonthlyQuota(), entry.Credits()) {
	case quota.AllowPlan:
		s.chargeRequest(entry)
		return nil
	case quota.AllowCredits:
		return s.spendCredit(ctx, entry)
	default:
		return failQuotaExceeded
	}
}

// spendCredit synchronously decrements one prepaid credit (billing-grade write,
// unlike the batched request metering).
func (s *Server) spendCredit(ctx context.Context, entry *keys.Entry) *apiError {
	balance, spent, err := s.auth.ConsumeCredit(ctx, entry.KeyID())
	if err != nil {
		s.log.Error("credit decrement failed", "error", err, "api_key_id", entry.KeyID())
		return failDependencyDown
	}
	if !spent {
		entry.SetCredits(0)
		return failQuotaExceeded
	}
	entry.SetCredits(balance)
	s.chargeRequest(entry)
	return nil
}

func (s *Server) chargeRequest(entry *keys.Entry) {
	entry.CountRequest()
	s.meter.Record(entry.KeyID())
}

// rawKeyFromRequest accepts Authorization: Bearer <key> or X-Api-Key: <key>.
func rawKeyFromRequest(r *http.Request) string {
	const bearerPrefix = "Bearer "
	if auth := r.Header.Get("Authorization"); strings.HasPrefix(auth, bearerPrefix) {
		return strings.TrimSpace(auth[len(bearerPrefix):])
	}
	return strings.TrimSpace(r.Header.Get("X-Api-Key"))
}
