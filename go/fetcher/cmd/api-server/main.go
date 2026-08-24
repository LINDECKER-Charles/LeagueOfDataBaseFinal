package main

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"lodb/go/fetcher/internal/api"
	"lodb/go/fetcher/internal/config"
	"lodb/go/fetcher/internal/fetcher"
)

// Server-side budgets. Named rather than inlined so the relationship between
// them stays readable: a batch may legitimately hold the response open for a
// long time, which is why WriteTimeout is far above ReadHeaderTimeout.
const (
	readHeaderTimeout = 5 * time.Second
	writeTimeout      = 60 * time.Second
	idleTimeout       = 90 * time.Second
	shutdownGrace     = 10 * time.Second
)

func main() {
	logger := newLogger()
	cfg := config.Load()
	// Fail fast: a gateway that starts healthy but refuses every fetch is far
	// harder to diagnose than a container that never comes up.
	if err := cfg.Validate(); err != nil {
		logger.Error("fetch.config.invalid", "error", err)
		os.Exit(1)
	}

	srv := newHTTPServer(cfg, logger)
	go listen(srv, cfg, logger)
	awaitTermination()
	shutdown(srv, logger)
}

// newLogger emits JSON on STDOUT. The collector tags every line with the stream
// it came from, so keeping application events on stdout leaves stderr as a
// reliable "something escaped the logger" channel (runtime panics, the Go
// runtime itself). This service used to write everything to stderr, which made a
// stream:stderr filter useless for telling an error from normal traffic.
func newLogger() *slog.Logger {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{
		Level: slog.LevelInfo,
	}))
	// Also re-points the stdlib log package (config.go still uses it, and so do
	// third-party packages): no line of this process can escape as plain text.
	slog.SetDefault(logger)
	return logger
}

// newHTTPServer assembles the gateway handler and applies the budgets above.
func newHTTPServer(cfg config.Config, logger *slog.Logger) *http.Server {
	// Idle pool sized to the batch concurrency: DDragon is HTTP/1.1, so each
	// concurrent fetch needs its own reusable keep-alive connection.
	f := fetcher.New(fetcher.Options{
		AllowedHosts:   cfg.AllowedHosts,
		Timeout:        cfg.RequestTimeout,
		MaxIdlePerHost: cfg.MaxConcurrency,
		MaxBodyBytes:   cfg.MaxResponseBytes,
	})
	handler := api.NewServer(api.Options{
		Fetcher:        f,
		DDragonBase:    cfg.DDragonBase,
		MaxConcurrency: cfg.MaxConcurrency,
		MaxURLs:        cfg.MaxURLsPerRequest,
		Log:            logger,
	})
	return &http.Server{
		Addr:              cfg.Addr(),
		Handler:           handler,
		ReadHeaderTimeout: readHeaderTimeout,
		WriteTimeout:      writeTimeout,
		IdleTimeout:       idleTimeout,
		// Without this, net/http writes handler panics and TLS errors as plain
		// text outside the JSON stream, and a stack trace becomes as many events
		// as it has lines.
		ErrorLog: slog.NewLogLogger(logger.Handler(), slog.LevelError),
	}
}

func listen(srv *http.Server, cfg config.Config, logger *slog.Logger) {
	logger.Info("fetch.server.listening",
		"addr", cfg.Addr(), "allowed_hosts", cfg.AllowedHosts)
	if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		logger.Error("fetch.server.failed", "error", err)
		os.Exit(1)
	}
}

func awaitTermination() {
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	<-stop
}

func shutdown(srv *http.Server, logger *slog.Logger) {
	ctx, cancel := context.WithTimeout(context.Background(), shutdownGrace)
	defer cancel()
	if err := srv.Shutdown(ctx); err != nil {
		logger.Error("fetch.server.shutdown_failed", "error", err)
	}
	logger.Info("fetch.server.stopped")
}
