package api

import (
	"net/http"
	"net/http/httptest"
	"testing"

	"leagueofdatabase/go-api/internal/store"
)

// publicContent answers every content query without failing.
func publicContent() *stubContent {
	return &stubContent{profile: store.Profile{ID: 1, Username: "someone", IsPublic: true}}
}

func TestRequestWithoutKeyIsRejected(t *testing.T) {
	handler := testServer(&stubAuth{key: usableKey()}, publicContent(), &countingMeter{})
	rec := httptest.NewRecorder()
	handler.ServeHTTP(rec, httptest.NewRequest(http.MethodGet, "/v1/profiles/someone", nil))
	assertStatus(t, rec, http.StatusUnauthorized)
}

func TestMalformedKeyIsRejectedBeforeAnyLookup(t *testing.T) {
	auth := &stubAuth{key: usableKey(), keyErr: errDatabaseDown}
	req := httptest.NewRequest(http.MethodGet, "/v1/profiles/someone", nil)
	req.Header.Set("X-Api-Key", "not-a-key")
	rec := httptest.NewRecorder()
	testServer(auth, publicContent(), &countingMeter{}).ServeHTTP(rec, req)
	// A store outage would surface as 503; 401 proves the store was never asked.
	assertStatus(t, rec, http.StatusUnauthorized)
}

func TestUnknownKeyIsRejected(t *testing.T) {
	auth := &stubAuth{keyErr: store.ErrNotFound}
	rec := authedGet(testServer(auth, publicContent(), &countingMeter{}), "/v1/profiles/someone")
	assertStatus(t, rec, http.StatusUnauthorized)
}

func TestRevokedKeyIsForbidden(t *testing.T) {
	key := usableKey()
	key.Active = false
	rec := authedGet(testServer(&stubAuth{key: key}, publicContent(), &countingMeter{}),
		"/v1/profiles/someone")
	assertStatus(t, rec, http.StatusForbidden)
}

func TestKeyLookupOutageIsReportedAsUnavailable(t *testing.T) {
	auth := &stubAuth{keyErr: errDatabaseDown}
	rec := authedGet(testServer(auth, publicContent(), &countingMeter{}), "/v1/profiles/someone")
	assertStatus(t, rec, http.StatusServiceUnavailable)
}

// The X-RateLimit-* headers describe the bucket on refusals too, otherwise a
// throttled client has no way to know when to retry.
func TestRateLimitRefusalStillAdvertisesTheBucket(t *testing.T) {
	key := usableKey()
	key.RateLimitPerMin = 1
	handler := testServer(&stubAuth{key: key}, publicContent(), &countingMeter{})

	assertStatus(t, authedGet(handler, "/v1/profiles/someone"), http.StatusOK)
	refused := authedGet(handler, "/v1/profiles/someone")
	assertStatus(t, refused, http.StatusTooManyRequests)
	advertised := []string{"X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset"}
	for _, header := range advertised {
		if refused.Header().Get(header) == "" {
			t.Errorf("%s missing from the 429 response", header)
		}
	}
}

func TestExhaustedQuotaWithoutCreditsIsRefused(t *testing.T) {
	key := usableKey()
	key.MonthlyQuota = 5
	meter := &countingMeter{}
	auth := &stubAuth{key: key, used: 5}
	rec := authedGet(testServer(auth, publicContent(), meter), "/v1/profiles/someone")
	assertStatus(t, rec, http.StatusTooManyRequests)
	if meter.charged != 0 {
		t.Fatalf("a refused request must not be metered, charged = %d", meter.charged)
	}
}

func TestExhaustedQuotaFallsBackToPrepaidCredits(t *testing.T) {
	key := usableKey()
	key.MonthlyQuota = 5
	key.CreditsBalance = 3
	meter := &countingMeter{}
	auth := &stubAuth{key: key, used: 5, spent: true, balance: 2}
	rec := authedGet(testServer(auth, publicContent(), meter), "/v1/profiles/someone")
	assertStatus(t, rec, http.StatusOK)
	if meter.charged != 1 {
		t.Fatalf("charged = %d, want 1", meter.charged)
	}
}

func TestCreditDecrementOutageIsReportedAsUnavailable(t *testing.T) {
	key := usableKey()
	key.MonthlyQuota = 5
	key.CreditsBalance = 3
	auth := &stubAuth{key: key, used: 5, spendErr: errDatabaseDown}
	rec := authedGet(testServer(auth, publicContent(), &countingMeter{}), "/v1/profiles/someone")
	assertStatus(t, rec, http.StatusServiceUnavailable)
}

// /v1/usage opts out of quota enforcement so an exhausted key can still see why.
func TestUsageStaysReachableOnAnExhaustedKey(t *testing.T) {
	key := usableKey()
	key.MonthlyQuota = 5
	auth := &stubAuth{
		key:      key,
		used:     5,
		snapshot: store.UsageSnapshot{Plan: "pro", MonthlyQuota: 5, UsedThisMonth: 9},
	}
	rec := authedGet(testServer(auth, publicContent(), &countingMeter{}), "/v1/usage")
	assertStatus(t, rec, http.StatusOK)
}
