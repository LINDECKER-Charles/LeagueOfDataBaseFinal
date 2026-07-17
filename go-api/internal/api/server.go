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

	"leagueofdatabase/go-api/internal/keys"
	"leagueofdatabase/go-api/internal/ratelimit"
	"leagueofdatabase/go-api/internal/store"
	"leagueofdatabase/go-api/internal/trends"
)

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
	PublicBuilds(ctx context.Context, championID string, limit, offset int) ([]store.Build, int64, error)
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
	auth     AuthStore
	content  ContentStore
	trends   *trends.Service
	keyCache *keys.Cache
	limiter  *ratelimit.Limiter
	meter    UsageMeter
	pgPing   Pinger
	s3Ping   Pinger
	log      *slog.Logger
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
	Log      *slog.Logger
}

// NewServer builds the routed handler with CORS + request logging applied.
func NewServer(d Deps) http.Handler {
	s := &Server{
		auth: d.Auth, content: d.Content, trends: d.Trends, keyCache: d.KeyCache,
		limiter: d.Limiter, meter: d.Meter, pgPing: d.PGPing, s3Ping: d.S3Ping, log: d.Log,
	}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", s.handleHealth)
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
		next.ServeHTTP(rec, r)
		s.log.Info("request",
			"method", r.Method, "path", r.URL.Path,
			"status", rec.status, "duration_ms", time.Since(start).Milliseconds())
	})
}

func isNotFound(err error) bool { return errors.Is(err, store.ErrNotFound) }
