package api

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"leagueofdatabase/go-api/internal/keys"
	"leagueofdatabase/go-api/internal/ratelimit"
	"leagueofdatabase/go-api/internal/store"
	"leagueofdatabase/go-api/internal/trends"
)

// testRawKey is the only shape keys.Hash accepts: lodb_ + 40 hex characters.
const testRawKey = "lodb_" + "0123456789abcdef0123456789abcdef01234567"

const testSiteBaseURL = "https://example.test"

// errDatabaseDown stands for any transient persistence failure.
var errDatabaseDown = errors.New("database down")

// stubAuth is an in-memory AuthStore whose every call can be made to fail.
type stubAuth struct {
	key      keys.APIKey
	keyErr   error
	used     int64
	usedErr  error
	spendErr error
	spent    bool
	balance  int64
	snapshot store.UsageSnapshot
	snapErr  error
}

func (s *stubAuth) KeyByHash(context.Context, string) (keys.APIKey, error) {
	return s.key, s.keyErr
}

func (s *stubAuth) MonthlyUsage(context.Context, int) (int64, error) {
	return s.used, s.usedErr
}

func (s *stubAuth) ConsumeCredit(context.Context, int) (int64, bool, error) {
	return s.balance, s.spent, s.spendErr
}

func (s *stubAuth) UsageSnapshot(context.Context, int) (store.UsageSnapshot, error) {
	return s.snapshot, s.snapErr
}

// stubContent is an in-memory ContentStore whose every call can be made to fail.
type stubContent struct {
	profile    store.Profile
	profileErr error
	buildCount int64
	countErr   error
	builds     []store.Build
	total      int64
	buildsErr  error
}

func (s *stubContent) ProfileByUsername(context.Context, string) (store.Profile, error) {
	return s.profile, s.profileErr
}

func (s *stubContent) CountPublicBuilds(context.Context, int) (int64, error) {
	return s.buildCount, s.countErr
}

func (s *stubContent) PublicBuilds(
	context.Context, store.BuildsQuery,
) ([]store.Build, int64, error) {
	return s.builds, s.total, s.buildsErr
}

// countingMeter records the billable requests the pipeline charged.
type countingMeter struct{ charged int }

func (m *countingMeter) Record(int) { m.charged++ }

type okPinger struct{}

func (okPinger) Ping(context.Context) error { return nil }

// usableKey is a healthy key with room in its plan, unless a test narrows it.
func usableKey() keys.APIKey {
	return keys.APIKey{
		ID:              42,
		Plan:            "pro",
		MonthlyQuota:    1000,
		RateLimitPerMin: 60,
		Active:          true,
	}
}

// testServer wires a Server over stubs, exercising the real routing/middleware.
func testServer(auth *stubAuth, content *stubContent, meter UsageMeter) http.Handler {
	return NewServer(Deps{
		Auth:     auth,
		Content:  content,
		Trends:   trends.New(trends.Options{Reader: emptyDaily{}, Names: noNames{}}),
		KeyCache: keys.NewCache(time.Minute, nil),
		Limiter:  ratelimit.New(nil),
		Meter:    meter,
		PGPing:   okPinger{},
		S3Ping:   okPinger{},

		SiteBaseURL: testSiteBaseURL,

		Log: slog.New(slog.DiscardHandler),
	})
}

type emptyDaily struct{}

func (emptyDaily) ReadDaily(context.Context, string) ([]byte, error) {
	return nil, errors.New("no rollup")
}

type noNames struct{}

func (noNames) Names(context.Context, string) map[string]string { return map[string]string{} }

// authedGet issues an authenticated GET against the routed handler.
func authedGet(handler http.Handler, target string) *httptest.ResponseRecorder {
	req := httptest.NewRequest(http.MethodGet, target, nil)
	req.Header.Set("Authorization", "Bearer "+testRawKey)
	rec := httptest.NewRecorder()
	handler.ServeHTTP(rec, req)
	return rec
}

func assertStatus(t *testing.T, rec *httptest.ResponseRecorder, want int) {
	t.Helper()
	if rec.Code != want {
		t.Fatalf("status = %d, want %d (body: %s)",
			rec.Code, want, strings.TrimSpace(rec.Body.String()))
	}
}
