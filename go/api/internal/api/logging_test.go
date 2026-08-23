package api

import (
	"bytes"
	"encoding/json"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"lodb/go/api/internal/keys"
	"lodb/go/api/internal/ratelimit"
	"lodb/go/api/internal/store"
	"lodb/go/api/internal/trends"
)

// accessRecord is one decoded JSON log line of the access stream.
type accessRecord struct {
	Msg      string `json:"msg"`
	Level    string `json:"level"`
	Path     string `json:"path"`
	Status   int    `json:"status"`
	APIKeyID int    `json:"api_key_id"`
	Duration *int64 `json:"duration_ms"`
}

// capturingServer wires the real routing/middleware over a readable log stream.
func capturingServer(t *testing.T, auth *stubAuth) (http.Handler, func() []accessRecord) {
	t.Helper()
	var buf bytes.Buffer
	handler := NewServer(Deps{
		Auth:        auth,
		Content:     publicContent(),
		Trends:      trends.New(trends.Options{Reader: emptyDaily{}, Names: noNames{}}),
		KeyCache:    keys.NewCache(time.Minute, nil),
		Limiter:     ratelimit.New(nil),
		Meter:       &countingMeter{},
		PGPing:      okPinger{},
		S3Ping:      okPinger{},
		SiteBaseURL: testSiteBaseURL,
		Log:         slog.New(slog.NewJSONHandler(&buf, nil)),
	})

	return handler, func() []accessRecord {
		var out []accessRecord
		for _, line := range strings.Split(strings.TrimSpace(buf.String()), "\n") {
			if line == "" {
				continue
			}
			var r accessRecord
			if err := json.Unmarshal([]byte(line), &r); err != nil {
				t.Fatalf("log line is not JSON: %q (%v)", line, err)
			}
			out = append(out, r)
		}
		return out
	}
}

func onlyAccess(t *testing.T, records []accessRecord) accessRecord {
	t.Helper()
	var found []accessRecord
	for _, r := range records {
		if r.Msg == "http.request.served" {
			found = append(found, r)
		}
	}
	if len(found) != 1 {
		t.Fatalf("want exactly one access record, got %d", len(found))
	}
	return found[0]
}

// The access line must name WHICH key served the request — an internal id that
// joins back to api_keys. The key itself is a credential and never travels.
func TestTheAccessLineCarriesTheKeyIdAndNotTheKey(t *testing.T) {
	handler, logs := capturingServer(t, &stubAuth{key: usableKey()})

	rec := authedGet(handler, "/v1/profiles/someone")
	assertStatus(t, rec, http.StatusOK)

	r := onlyAccess(t, logs())
	if r.APIKeyID != usableKey().ID {
		t.Fatalf("api_key_id = %d, want %d", r.APIKeyID, usableKey().ID)
	}
	if r.Duration == nil {
		t.Fatal("duration_ms must be a NUMBER, not a rendered time.Duration")
	}
}

func TestNoRawKeyEverReachesTheLogStream(t *testing.T) {
	var buf bytes.Buffer
	handler := NewServer(Deps{
		Auth:        &stubAuth{key: usableKey()},
		Content:     publicContent(),
		Trends:      trends.New(trends.Options{Reader: emptyDaily{}, Names: noNames{}}),
		KeyCache:    keys.NewCache(time.Minute, nil),
		Limiter:     ratelimit.New(nil),
		Meter:       &countingMeter{},
		PGPing:      okPinger{},
		S3Ping:      okPinger{},
		SiteBaseURL: testSiteBaseURL,
		Log:         slog.New(slog.NewJSONHandler(&buf, nil)),
	})

	authedGet(handler, "/v1/profiles/someone")

	if strings.Contains(buf.String(), testRawKey) {
		t.Fatal("the raw API key reached the log stream")
	}
}

// Every request used to be logged at INFO, 5xx included, which made the level
// column useless for spotting a failing service.
func TestTheLevelFollowsTheStatus(t *testing.T) {
	cases := []struct {
		name  string
		auth  *stubAuth
		want  string
		total int
	}{
		{"a served request", &stubAuth{key: usableKey()}, "INFO", http.StatusOK},
		{"a refused key", &stubAuth{keyErr: store.ErrNotFound}, "WARN", http.StatusUnauthorized},
		{"a dependency outage", &stubAuth{keyErr: errDatabaseDown}, "ERROR", http.StatusServiceUnavailable},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			handler, logs := capturingServer(t, tc.auth)

			rec := authedGet(handler, "/v1/profiles/someone")
			assertStatus(t, rec, tc.total)

			if r := onlyAccess(t, logs()); r.Level != tc.want {
				t.Fatalf("level = %q for %d, want %q", r.Level, tc.total, tc.want)
			}
		})
	}
}

// The container healthcheck probes every 15s — 5 760 lines a day, per stack,
// kept 90 days, saying nothing.
func TestTheHealthProbeIsNotJournalled(t *testing.T) {
	handler, logs := capturingServer(t, &stubAuth{key: usableKey()})

	handler.ServeHTTP(
		httptest.NewRecorder(),
		httptest.NewRequest(http.MethodGet, healthPath, nil),
	)

	for _, r := range logs() {
		if r.Msg == "http.request.served" {
			t.Fatal("the health probe must not produce an access line")
		}
	}
}

// A path is caller-controlled: a newline in it would inject whole log records,
// each of them level-less and key-less.
func TestAControlCharacterInThePathCannotInjectALine(t *testing.T) {
	handler, logs := capturingServer(t, &stubAuth{key: usableKey()})

	req := httptest.NewRequest(http.MethodGet, "/v1/profiles/someone", nil)
	req.Header.Set("Authorization", "Bearer "+testRawKey)
	req.URL.Path = "/v1/profiles/some\none"
	handler.ServeHTTP(httptest.NewRecorder(), req)

	for _, r := range logs() {
		if r.Msg == "forged" {
			t.Fatal("a forged record was injected through the path")
		}
		if strings.ContainsAny(r.Path, "\n\r") {
			t.Fatalf("path %q still carries a control character", r.Path)
		}
	}
}
