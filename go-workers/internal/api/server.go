// Package api exposes the go-fetcher HTTP surface: health, DDragon passthroughs
// and a bounded parallel batch fetch.
package api

import (
	"context"
	"encoding/json"
	"log"
	"net/http"
	"time"

	"go-workers/internal/fetcher"
)

// URLFetcher is the subset of *fetcher.Fetcher the server needs (stubbable in tests).
type URLFetcher interface {
	Fetch(ctx context.Context, url string) (fetcher.Result, error)
}

// Server wires the HTTP API over a URLFetcher.
type Server struct {
	fetcher        URLFetcher
	ddragonBase    string
	maxConcurrency int
	maxURLs        int
}

// Options groups what the server needs. Grouped in a struct rather than passed
// positionally: the knobs are unrelated to one another and a bare argument list
// would not say which is which at the call site.
type Options struct {
	Fetcher URLFetcher
	// DDragonBase is the origin of the /versions and /languages passthroughs.
	// It comes from the configuration so it cannot drift from the SSRF allowlist.
	DDragonBase    string
	MaxConcurrency int
	MaxURLs        int
}

// NewServer builds the routed, request-logging HTTP handler.
func NewServer(opts Options) http.Handler {
	s := &Server{
		fetcher:        opts.Fetcher,
		ddragonBase:    opts.DDragonBase,
		maxConcurrency: opts.MaxConcurrency,
		maxURLs:        opts.MaxURLs,
	}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", s.handleHealth)
	mux.HandleFunc("GET /versions", s.handleVersions)
	mux.HandleFunc("GET /languages", s.handleLanguages)
	mux.HandleFunc("POST /fetch", s.handleFetch)
	return logRequests(mux)
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}

func writeError(w http.ResponseWriter, status int, msg string) {
	writeJSON(w, status, map[string]string{"error": msg})
}

type statusRecorder struct {
	http.ResponseWriter
	status int
}

func (r *statusRecorder) WriteHeader(code int) {
	r.status = code
	r.ResponseWriter.WriteHeader(code)
}

func logRequests(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		rec := &statusRecorder{ResponseWriter: w, status: http.StatusOK}
		next.ServeHTTP(rec, r)
		log.Printf("%s %s %d %s", r.Method, r.URL.Path, rec.status, time.Since(start))
	})
}
