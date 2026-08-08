package api

import (
	"context"
	"encoding/base64"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"lodb/go/fetcher/internal/fetcher"
)

type stubFetcher struct {
	fn func(ctx context.Context, url string) (fetcher.Result, error)
}

func (s stubFetcher) Fetch(ctx context.Context, url string) (fetcher.Result, error) {
	return s.fn(ctx, url)
}

func okFetcher() stubFetcher {
	return stubFetcher{fn: func(_ context.Context, url string) (fetcher.Result, error) {
		return fetcher.Result{
			Body:        []byte("body:" + url),
			ContentType: "application/json",
			Status:      200,
		}, nil
	}}
}

const testDDragonBase = "https://ddragon.leagueoflegends.com"

func newTestServer(f URLFetcher, maxConcurrency, maxURLs int) http.Handler {
	return NewServer(Options{
		Fetcher:        f,
		DDragonBase:    testDDragonBase,
		MaxConcurrency: maxConcurrency,
		MaxURLs:        maxURLs,
	})
}

func TestHealth(t *testing.T) {
	srv := newTestServer(okFetcher(), 4, 10)
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodGet, "/healthz", nil))
	if rec.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200", rec.Code)
	}
}

func TestFetchBatchPreservesOrder(t *testing.T) {
	srv := newTestServer(okFetcher(), 4, 10)
	body := `{"urls":["https://ddragon.leagueoflegends.com/a.json",` +
		`"https://ddragon.leagueoflegends.com/b.json"]}`
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/fetch", strings.NewReader(body)))

	if rec.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200", rec.Code)
	}
	var resp fetchResponse
	if err := json.NewDecoder(rec.Body).Decode(&resp); err != nil {
		t.Fatal(err)
	}
	if len(resp.Results) != 2 {
		t.Fatalf("results = %d, want 2", len(resp.Results))
	}
	for i, want := range []string{"a.json", "b.json"} {
		got, err := base64.StdEncoding.DecodeString(resp.Results[i].BodyBase64)
		if err != nil {
			t.Fatalf("result[%d] base64: %v", i, err)
		}
		if !strings.Contains(string(got), want) {
			t.Errorf("result[%d] body = %q, want contains %q", i, got, want)
		}
	}
}

func TestFetchTooManyURLs(t *testing.T) {
	srv := newTestServer(okFetcher(), 4, 1)
	body := `{"urls":["https://ddragon.leagueoflegends.com/a",` +
		`"https://ddragon.leagueoflegends.com/b"]}`
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/fetch", strings.NewReader(body)))
	if rec.Code != http.StatusRequestEntityTooLarge {
		t.Fatalf("status = %d, want 413", rec.Code)
	}
}

func TestFetchInvalidJSON(t *testing.T) {
	srv := newTestServer(okFetcher(), 4, 10)
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/fetch", strings.NewReader("not json")))
	if rec.Code != http.StatusBadRequest {
		t.Fatalf("status = %d, want 400", rec.Code)
	}
}

// The passthrough origin must come from configuration, not from a constant
// baked into the binary: it has to stay in step with the SSRF allowlist.
func TestPassthroughsUseTheConfiguredDDragonBase(t *testing.T) {
	var called []string
	capturing := stubFetcher{fn: func(_ context.Context, url string) (fetcher.Result, error) {
		called = append(called, url)
		return fetcher.Result{Body: []byte("[]"), ContentType: "application/json", Status: 200}, nil
	}}
	srv := NewServer(Options{
		Fetcher:        capturing,
		DDragonBase:    "https://mirror.test",
		MaxConcurrency: 1,
		MaxURLs:        1,
	})
	for _, path := range []string{"/versions", "/languages"} {
		srv.ServeHTTP(httptest.NewRecorder(), httptest.NewRequest(http.MethodGet, path, nil))
	}
	want := []string{
		"https://mirror.test/api/versions.json",
		"https://mirror.test/cdn/languages.json",
	}
	if len(called) != len(want) {
		t.Fatalf("fetched %v, want %v", called, want)
	}
	for i, url := range want {
		if called[i] != url {
			t.Errorf("call %d = %q, want %q", i, called[i], url)
		}
	}
}

// The 1 MiB ceiling is an inter-service contract with the PHP client; an
// oversized payload must be refused rather than buffered.
func TestFetchRejectsAnOversizedBody(t *testing.T) {
	srv := newTestServer(okFetcher(), 4, 10)
	payload := `{"urls":["` + strings.Repeat("a", int(MaxRequestBodyBytes)) + `"]}`
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/fetch", strings.NewReader(payload)))
	if rec.Code != http.StatusBadRequest {
		t.Fatalf("status = %d, want 400", rec.Code)
	}
}
