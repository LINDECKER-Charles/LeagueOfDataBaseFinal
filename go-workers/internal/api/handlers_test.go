package api

import (
	"context"
	"encoding/base64"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"go-workers/internal/fetcher"
)

type stubFetcher struct {
	fn func(ctx context.Context, url string) (fetcher.Result, error)
}

func (s stubFetcher) Fetch(ctx context.Context, url string) (fetcher.Result, error) {
	return s.fn(ctx, url)
}

func okFetcher() stubFetcher {
	return stubFetcher{fn: func(_ context.Context, url string) (fetcher.Result, error) {
		return fetcher.Result{Body: []byte("body:" + url), ContentType: "application/json", Status: 200}, nil
	}}
}

func TestHealth(t *testing.T) {
	srv := NewServer(okFetcher(), 4, 10)
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodGet, "/healthz", nil))
	if rec.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200", rec.Code)
	}
}

func TestFetchBatchPreservesOrder(t *testing.T) {
	srv := NewServer(okFetcher(), 4, 10)
	body := `{"urls":["https://ddragon.leagueoflegends.com/a.json","https://ddragon.leagueoflegends.com/b.json"]}`
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
	srv := NewServer(okFetcher(), 4, 1)
	body := `{"urls":["https://ddragon.leagueoflegends.com/a","https://ddragon.leagueoflegends.com/b"]}`
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/fetch", strings.NewReader(body)))
	if rec.Code != http.StatusRequestEntityTooLarge {
		t.Fatalf("status = %d, want 413", rec.Code)
	}
}

func TestFetchInvalidJSON(t *testing.T) {
	srv := NewServer(okFetcher(), 4, 10)
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/fetch", strings.NewReader("not json")))
	if rec.Code != http.StatusBadRequest {
		t.Fatalf("status = %d, want 400", rec.Code)
	}
}
