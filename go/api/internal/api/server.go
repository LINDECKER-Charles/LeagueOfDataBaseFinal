// Package api exposes the public LeagueOfDataBase API: authenticated /v1
// endpoints (profiles, builds, trends, usage) plus the unauthenticated /healthz.
package api

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"lodb/go/api/internal/keys"
	"lodb/go/api/internal/ratelimit"
	"lodb/go/api/internal/store"
	"lodb/go/api/internal/trends"
)

// healthPath is excluded from the access log: the container healthcheck probes
// it every 15s — 5 760 lines a day, per stack, kept 90 days, saying nothing.
const healthPath = "/healthz"

// maxLoggedValueBytes bounds a caller-controlled value before it reaches a log
// line, so one request cannot push a record past the collector's limits.
const maxLoggedValueBytes = 256

// AuthStore covers the key/quota persistence needed by the middleware.
type AuthStore interface {
	KeyByHash(ctx context.Context, hash string) (keys.APIKey, error)
	MonthlyUsage(ctx context.Context, keyID int) (int64, error)
	ConsumeCredit(ctx context.Context, keyID int) (balance int64, spent bool, err error)
	UsageSnapshot(ctx context.Context, keyID int) (store.UsageSnapshot, error)
}

// ContentStore covers the read-only user data behind profiles and builds.
type ContentStore interface {
	ProfileByUsername(ctx context.Context, username string) (store.Profile, error)
	CountPublicBuilds(ctx context.Context, userID int) (int64, error)
	PublicBuilds(ctx context.Context, q store.BuildsQuery) ([]store.Build, int64, error)
}

// Pinger reports dependency reachability for /healthz.
type Pinger interface {
	Ping(ctx context.Context) error
}

// UsageMeter records billable requests (batched persistence).
type UsageMeter interface {
	Record(keyID int)
}

// Server wires the HTTP surface over its dependencies.
type Server struct {
	auth        AuthStore
	content     ContentStore
	trends      *trends.Service
	keyCache    *keys.Cache
	limiter     *ratelimit.Limiter
	meter       UsageMeter
	pgPing      Pinger
	s3Ping      Pinger
	siteBaseURL string
	log         *slog.Logger
}

// Deps groups the server dependencies (constructor object, DI-style).
type Deps struct {
	Auth     AuthStore
	Content  ContentStore
	Trends   *trends.Service
	KeyCache *keys.Cache
	Limiter  *ratelimit.Limiter
	Meter    UsageMeter
	PGPing   Pinger
	S3Ping   Pinger
	// SiteBaseURL is the public origin of the website, used to render absolute
	// links (build sharing) in responses served to third-party clients.
	SiteBaseURL string
	Log         *slog.Logger
}

// NewServer builds the routed handler with CORS + request logging applied.
func NewServer(d Deps) http.Handler {
	s := &Server{
		auth: d.Auth, content: d.Content, trends: d.Trends, keyCache: d.KeyCache,
		limiter: d.Limiter, meter: d.Meter, pgPing: d.PGPing, s3Ping: d.S3Ping,
		siteBaseURL: d.SiteBaseURL, log: d.Log,
	}
	mux := http.NewServeMux()
	mux.HandleFunc("GET "+healthPath, s.handleHealth)
	mux.HandleFunc("GET /v1/profiles/{username}", s.withAuth(true, s.handleProfile))
	mux.HandleFunc("GET /v1/champions/{championId}/builds", s.withAuth(true, s.handleChampionBuilds))
	mux.HandleFunc("GET /v1/trends/{type}", s.withAuth(true, s.handleTrends))
	mux.HandleFunc("GET /v1/usage", s.withAuth(false, s.handleUsage))
	return s.logRequests(corsPublic(mux))
}

// corsPublic opens /v1 to browsers: it is a public, key-authenticated API.
func corsPublic(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasPrefix(r.URL.Path, "/v1/") {
			header := w.Header()
			header.Set("Access-Control-Allow-Origin", "*")
			header.Set("Access-Control-Allow-Methods", "GET, OPTIONS")
			header.Set("Access-Control-Allow-Headers", "Authorization, X-Api-Key")
			if r.Method == http.MethodOptions {
				w.WriteHeader(http.StatusNoContent)
				return
			}
		}
		next.ServeHTTP(w, r)
	})
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
		// The authenticated key is resolved DEEPER in the chain, on a derived
		// request this closure never sees. A holder installed on the way in is how
		// the access line can name the caller on the way out.
		caller := &callerInfo{}
		next.ServeHTTP(rec, r.WithContext(withCaller(r.Context(), caller)))
		if r.URL.Path == healthPath {
			return
		}
		attrs := []slog.Attr{
			slog.String("method", r.Method),
			slog.String("path", safeValue(r.URL.Path)),
			slog.Int("status", rec.status),
			slog.Int64("duration_ms", time.Since(start).Milliseconds()),
		}
		// The internal id only. The key itself is a credential and never travels.
		if caller.apiKeyID != 0 {
			attrs = append(attrs, slog.Int("api_key_id", caller.apiKeyID))
		}
		s.log.LogAttrs(r.Context(), levelForStatus(rec.status), "http.request.served", attrs...)
	})
}

// levelForStatus keeps the level tied to the outcome. Every request used to be
// logged at INFO, 5xx included, which made the level column useless for spotting
// a failing service.
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

func isNotFound(err error) bool { return errors.Is(err, store.ErrNotFound) }

// storeFailure reports a persistence-layer failure. Every error the stores can
// still surface here means the dependency is down (or unmigrated), which is a
// 503 — writeInternal stays reserved for genuine internal faults, so the status
// keeps telling supervision apart "our bug" from "database outage".
func (s *Server) storeFailure(w http.ResponseWriter, msg string, err error) {
	s.log.Error(msg, "error", err)
	writeUnavailable(w)
}
