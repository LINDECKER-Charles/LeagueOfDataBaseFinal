// Package api exposes the go-fetcher HTTP surface: health, DDragon passthroughs
// and a bounded parallel batch fetch.
package api

import (
	"context"
	"encoding/json"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"lodb/go/fetcher/internal/fetcher"
)

// healthPath is excluded from the access log: the container healthcheck probes
// it every 15s — 5 760 lines a day, per stack, kept 90 days, saying nothing.
const healthPath = "/healthz"

// maxLoggedValueBytes bounds an attacker-influenced value before it reaches a
// log line, so one request cannot push a record past the collector's limits.
const maxLoggedValueBytes = 256

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
	log            *slog.Logger
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
	// Log defaults to slog.Default(), which main() has already pointed at stdout
	// in JSON. Injected so a test can discard instead of polluting its output.
	Log *slog.Logger
}

// NewServer builds the routed, request-logging HTTP handler.
func NewServer(opts Options) http.Handler {
	logger := opts.Log
	if logger == nil {
		logger = slog.Default()
	}
	s := &Server{
		fetcher:        opts.Fetcher,
		ddragonBase:    opts.DDragonBase,
		maxConcurrency: opts.MaxConcurrency,
		maxURLs:        opts.MaxURLs,
		log:            logger,
	}
	mux := http.NewServeMux()
	mux.HandleFunc("GET "+healthPath, s.handleHealth)
	mux.HandleFunc("GET /versions", s.handleVersions)
	mux.HandleFunc("GET /languages", s.handleLanguages)
	mux.HandleFunc("POST /fetch", s.handleFetch)
	return s.logRequests(mux)
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

func (s *Server) logRequests(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		rec := &statusRecorder{ResponseWriter: w, status: http.StatusOK}
		next.ServeHTTP(rec, r)
		if r.URL.Path == healthPath {
			return
		}
		s.log.LogAttrs(r.Context(), levelForStatus(rec.status), "http.request.served",
			slog.String("method", r.Method),
			slog.String("path", safeValue(r.URL.Path)),
			slog.Int("status", rec.status),
			// Milliseconds as a NUMBER: time.Duration.String() renders "1.481s"
			// then "532ms", which no graph can plot.
			slog.Int64("duration_ms", time.Since(start).Milliseconds()),
		)
	})
}

// levelForStatus keeps the level tied to the outcome. A 5xx logged at INFO — as
// go-api used to — makes the level column useless for spotting a failing service.
func levelForStatus(status int) slog.Level {
	switch {
	case status >= http.StatusInternalServerError:
		return slog.LevelError
	case status >= http.StatusBadRequest:
		return slog.LevelWarn
	default:
		return slog.LevelInfo
	}
}

// safeValue makes a caller-controlled string safe to journal.
//
// The collector builds ONE event per physical line: a value carrying a newline
// would inject whole records, the rest of them level-less and key-less. slog's
// JSON encoder escapes control characters today, but the sanitising belongs to
// the value, not to whichever encoder happens to be wired underneath.
//
// It cannot do anything about the word "error" appearing inside a path, which
// the collector's regex reads as a level — that is precisely why no alert is
// ever built on the level field.
func safeValue(value string) string {
	if len(value) > maxLoggedValueBytes {
		value = value[:maxLoggedValueBytes]
	}
	return strings.Map(func(r rune) rune {
		if r < 0x20 || r == 0x7f {
			return '_'
		}
		return r
	}, value)
}
