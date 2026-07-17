package api

import (
	"context"
	"net/http"
	"strconv"
	"strings"

	"leagueofdatabase/go-api/internal/keys"
	"leagueofdatabase/go-api/internal/quota"
)

type entryContextKey struct{}

// entryFromContext returns the authenticated key entry stashed by withAuth.
func entryFromContext(ctx context.Context) *keys.Entry {
	entry, _ := ctx.Value(entryContextKey{}).(*keys.Entry)
	return entry
}

// withAuth authenticates and rate-limits a /v1 handler. enforceQuota is a
// documented orthogonal opt-out: /v1/usage stays reachable (and free) when the
// quota is exhausted so clients can always inspect their consumption.
func (s *Server) withAuth(enforceQuota bool, next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		entry := s.resolveKey(w, r)
		if entry == nil {
			return
		}
		if !s.applyRateLimit(w, entry) {
			return
		}
		if enforceQuota && !s.applyQuota(r.Context(), w, entry) {
			return
		}
		next(w, r.WithContext(context.WithValue(r.Context(), entryContextKey{}, entry)))
	}
}

// resolveKey extracts, validates and resolves the API key, writing the error
// response itself when authentication fails (nil return).
func (s *Server) resolveKey(w http.ResponseWriter, r *http.Request) *keys.Entry {
	raw := rawKeyFromRequest(r)
	if raw == "" {
		writeError(w, http.StatusUnauthorized, CodeUnauthorized, "missing API key: use Authorization: Bearer <key> or X-Api-Key")
		return nil
	}
	hash, err := keys.Hash(raw)
	if err != nil {
		writeError(w, http.StatusUnauthorized, CodeUnauthorized, "malformed API key")
		return nil
	}
	entry := s.keyCache.Get(hash)
	if entry == nil {
		entry = s.loadKey(r.Context(), w, hash)
		if entry == nil {
			return nil
		}
	}
	if entry.Invalid {
		writeError(w, http.StatusUnauthorized, CodeUnauthorized, "unknown API key")
		return nil
	}
	if !entry.Key.Usable() {
		writeError(w, http.StatusForbidden, CodeForbidden, "API key is revoked or inactive")
		return nil
	}
	return entry
}

// loadKey fills the cache from the database (positive or negative entry).
func (s *Server) loadKey(ctx context.Context, w http.ResponseWriter, hash string) *keys.Entry {
	key, err := s.auth.KeyByHash(ctx, hash)
	if err != nil {
		if isNotFound(err) {
			s.keyCache.PutInvalid(hash)
			writeError(w, http.StatusUnauthorized, CodeUnauthorized, "unknown API key")
			return nil
		}
		s.log.Error("api key lookup failed", "error", err)
		writeUnavailable(w)
		return nil
	}
	if !key.Usable() {
		// Cache the row itself (not a negative entry) so the verdict stays 403.
		return s.keyCache.PutValid(hash, key, 0)
	}
	used, err := s.auth.MonthlyUsage(ctx, key.ID)
	if err != nil {
		s.log.Error("monthly usage lookup failed", "error", err, "api_key_id", key.ID)
		writeUnavailable(w)
		return nil
	}
	return s.keyCache.PutValid(hash, key, used)
}

// applyRateLimit consumes a token and always sets the X-RateLimit-* headers.
func (s *Server) applyRateLimit(w http.ResponseWriter, entry *keys.Entry) bool {
	verdict := s.limiter.Allow(entry.Key.ID, entry.Key.RateLimitPerMin)
	header := w.Header()
	header.Set("X-RateLimit-Limit", strconv.Itoa(verdict.Limit))
	header.Set("X-RateLimit-Remaining", strconv.Itoa(verdict.Remaining))
	header.Set("X-RateLimit-Reset", strconv.FormatInt(verdict.Reset, 10))
	if !verdict.Allowed {
		writeError(w, http.StatusTooManyRequests, CodeRateLimited, "rate limit exceeded, retry after X-RateLimit-Reset")
	}
	return verdict.Allowed
}

// applyQuota charges the request to the monthly plan, then prepaid credits.
func (s *Server) applyQuota(ctx context.Context, w http.ResponseWriter, entry *keys.Entry) bool {
	switch quota.Evaluate(entry.MonthlyUsed(), entry.Key.MonthlyQuota, entry.Credits()) {
	case quota.AllowPlan:
		s.chargeRequest(entry)
		return true
	case quota.AllowCredits:
		return s.spendCredit(ctx, w, entry)
	default:
		writeError(w, http.StatusTooManyRequests, CodeQuotaExceeded, "monthly quota exhausted and no credits left")
		return false
	}
}

// spendCredit synchronously decrements one prepaid credit (billing-grade write,
// unlike the batched request metering).
func (s *Server) spendCredit(ctx context.Context, w http.ResponseWriter, entry *keys.Entry) bool {
	balance, spent, err := s.auth.ConsumeCredit(ctx, entry.Key.ID)
	if err != nil {
		s.log.Error("credit decrement failed", "error", err, "api_key_id", entry.Key.ID)
		writeUnavailable(w)
		return false
	}
	if !spent {
		entry.SetCredits(0)
		writeError(w, http.StatusTooManyRequests, CodeQuotaExceeded, "monthly quota exhausted and no credits left")
		return false
	}
	entry.SetCredits(balance)
	s.chargeRequest(entry)
	return true
}

func (s *Server) chargeRequest(entry *keys.Entry) {
	entry.CountRequest()
	s.meter.Record(entry.Key.ID)
}

// rawKeyFromRequest accepts Authorization: Bearer <key> or X-Api-Key: <key>.
func rawKeyFromRequest(r *http.Request) string {
	const bearerPrefix = "Bearer "
	if auth := r.Header.Get("Authorization"); strings.HasPrefix(auth, bearerPrefix) {
		return strings.TrimSpace(auth[len(bearerPrefix):])
	}
	return strings.TrimSpace(r.Header.Get("X-Api-Key"))
}
