package api

import (
	"bytes"
	"context"
	"encoding/json"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"lodb/go/fetcher/internal/fetcher"
)

// record is one decoded JSON log line. Tests assert on the event key, the level
// and the attributes — never on the rendered text.
type record struct {
	Msg      string   `json:"msg"`
	Level    string   `json:"level"`
	Status   int      `json:"status"`
	Path     string   `json:"path"`
	Refused  int      `json:"refused"`
	Batch    int      `json:"batch"`
	Failed   int      `json:"failed"`
	Hosts    []string `json:"hosts"`
	Duration *int64   `json:"duration_ms"`
}

// capturingServer wires a server whose whole log stream is readable afterwards.
func capturingServer(t *testing.T, f URLFetcher) (http.Handler, func() []record) {
	t.Helper()
	var buf bytes.Buffer
	handler := NewServer(Options{
		Fetcher:        f,
		DDragonBase:    testDDragonBase,
		MaxConcurrency: 4,
		MaxURLs:        10,
		Log:            slog.New(slog.NewJSONHandler(&buf, nil)),
	})

	return handler, func() []record {
		var out []record
		for _, line := range strings.Split(strings.TrimSpace(buf.String()), "\n") {
			if line == "" {
				continue
			}
			var r record
			if err := json.Unmarshal([]byte(line), &r); err != nil {
				t.Fatalf("log line is not JSON: %q (%v)", line, err)
			}
			out = append(out, r)
		}
		return out
	}
}

func only(t *testing.T, records []record, key string) record {
	t.Helper()
	var found []record
	for _, r := range records {
		if r.Msg == key {
			found = append(found, r)
		}
	}
	if len(found) != 1 {
		t.Fatalf("want exactly one %q record, got %d in %v", key, len(found), keysOf(records))
	}
	return found[0]
}

func keysOf(records []record) []string {
	keys := make([]string, 0, len(records))
	for _, r := range records {
		keys = append(keys, r.Msg)
	}
	return keys
}

// refusingFetcher rejects every URL exactly like the real allowlist guard.
func refusingFetcher(err error) stubFetcher {
	return stubFetcher{fn: func(context.Context, string) (fetcher.Result, error) {
		return fetcher.Result{}, err
	}}
}

func postBatch(handler http.Handler, urls ...string) {
	body, _ := json.Marshal(fetchRequest{URLs: urls})
	handler.ServeHTTP(
		httptest.NewRecorder(),
		httptest.NewRequest(http.MethodPost, "/fetch", bytes.NewReader(body)),
	)
}

// The refusal is rendered IN-BAND inside an HTTP 200, so nothing outside the
// response body would ever see it. Either the caller builds URLs it must never
// build, or somebody is probing the gateway as an open proxy.
func TestAnAllowlistRefusalIsLoggedAsAnError(t *testing.T) {
	refusal := fetcher.ErrHostNotAllowed
	handler, logs := capturingServer(t, refusingFetcher(refusal))

	postBatch(handler, "https://evil.example.com/a", "https://evil.example.com/b")

	r := only(t, logs(), "fetch.allowlist.refused")
	if r.Level != "ERROR" {
		t.Fatalf("level = %q, want ERROR", r.Level)
	}
	if r.Refused != 2 || r.Batch != 2 {
		t.Fatalf("refused=%d batch=%d, want 2/2", r.Refused, r.Batch)
	}
	if len(r.Hosts) != 1 || r.Hosts[0] != "evil.example.com" {
		t.Fatalf("hosts = %v, want the refused host once", r.Hosts)
	}
}

// An allow-listed origin trying to relay us elsewhere is not the same event as a
// caller building a bad URL — and it wraps ErrHostNotAllowed, so ordering matters.
func TestARefusedRedirectIsItsOwnEvent(t *testing.T) {
	handler, logs := capturingServer(t, refusingFetcher(
		errWrap(fetcher.ErrRedirectNotAllowed, fetcher.ErrHostNotAllowed),
	))

	postBatch(handler, "https://ddragon.leagueoflegends.com/a.json")

	records := logs()
	only(t, records, "fetch.redirect.refused")
	for _, r := range records {
		if r.Msg == "fetch.allowlist.refused" {
			t.Fatal("a refused redirect must not also count as an allowlist refusal")
		}
	}
}

// One line for the whole batch, never one per URL: a cold champion list is a few
// hundred images in a single call.
func TestABatchIsSummarisedInASingleLine(t *testing.T) {
	handler, logs := capturingServer(t, refusingFetcher(fetcher.ErrHostNotAllowed))

	urls := make([]string, 0, 8)
	for range 8 {
		urls = append(urls, "https://evil.example.com/x")
	}
	postBatch(handler, urls...)

	r := only(t, logs(), "fetch.allowlist.refused")
	if r.Refused != 8 {
		t.Fatalf("refused = %d, want 8 counted on one line", r.Refused)
	}
}

// A transient upstream failure is a degradation, not a refusal.
func TestATransientFailureIsOnlyAWarning(t *testing.T) {
	handler, logs := capturingServer(t, refusingFetcher(errPlain("upstream 503")))

	postBatch(handler, "https://ddragon.leagueoflegends.com/a.json")

	r := only(t, logs(), "fetch.batch.degraded")
	if r.Level != "WARN" || r.Failed != 1 {
		t.Fatalf("level=%q failed=%d, want WARN/1", r.Level, r.Failed)
	}
}

// A healthy batch says nothing beyond its access line.
func TestAHealthyBatchOnlyLogsTheAccessLine(t *testing.T) {
	handler, logs := capturingServer(t, okFetcher())

	postBatch(handler, "https://ddragon.leagueoflegends.com/a.json")

	r := only(t, logs(), "http.request.served")
	if r.Status != http.StatusOK || r.Path != "/fetch" {
		t.Fatalf("status=%d path=%q, want 200 /fetch", r.Status, r.Path)
	}
	if r.Duration == nil {
		t.Fatal("duration_ms must be a NUMBER: time.Duration.String() cannot be plotted")
	}
}

// The container healthcheck probes every 15s — 5 760 lines a day, per stack,
// kept 90 days, saying nothing.
func TestTheHealthProbeIsNotJournalled(t *testing.T) {
	handler, logs := capturingServer(t, okFetcher())

	handler.ServeHTTP(
		httptest.NewRecorder(),
		httptest.NewRequest(http.MethodGet, healthPath, nil),
	)

	if records := logs(); len(records) != 0 {
		t.Fatalf("the health probe must be silent, got %v", keysOf(records))
	}
}

// A 4xx must not read as a normal request: the level follows the status.
func TestTheLevelFollowsTheStatus(t *testing.T) {
	handler, logs := capturingServer(t, okFetcher())

	handler.ServeHTTP(
		httptest.NewRecorder(),
		httptest.NewRequest(http.MethodPost, "/fetch", strings.NewReader("not json")),
	)

	if r := only(t, logs(), "http.request.served"); r.Level != "WARN" {
		t.Fatalf("level = %q for a 400, want WARN", r.Level)
	}
}

// A path is caller-controlled: a newline in it would inject whole log records,
// each of them level-less and key-less.
func TestAControlCharacterInThePathCannotInjectALine(t *testing.T) {
	handler, logs := capturingServer(t, okFetcher())

	req := httptest.NewRequest(http.MethodGet, "/versions", nil)
	req.URL.Path = "/versions\n{\"msg\":\"forged\"}"
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

// errPlain and errWrap keep the test fixtures free of a fmt/errors import dance.
func errPlain(msg string) error { return &plainError{msg} }

type plainError struct{ msg string }

func (e *plainError) Error() string { return e.msg }

func errWrap(outer, inner error) error { return &wrapped{outer, inner} }

type wrapped struct{ outer, inner error }

func (w *wrapped) Error() string { return w.outer.Error() + ": " + w.inner.Error() }

func (w *wrapped) Unwrap() []error { return []error{w.outer, w.inner} }
