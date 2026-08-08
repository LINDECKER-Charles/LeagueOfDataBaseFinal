package main

import (
	"context"
	"errors"
	"log"
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
	cfg := config.Load()
	// Fail fast: a gateway that starts healthy but refuses every fetch is far
	// harder to diagnose than a container that never comes up.
	if err := cfg.Validate(); err != nil {
		log.Fatalf("invalid configuration: %v", err)
	}

	srv := newHTTPServer(cfg)
	go listen(srv, cfg)
	awaitTermination()
	shutdown(srv)
}

// newHTTPServer assembles the gateway handler and applies the budgets above.
func newHTTPServer(cfg config.Config) *http.Server {
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
	})
	return &http.Server{
		Addr:              cfg.Addr(),
		Handler:           handler,
		ReadHeaderTimeout: readHeaderTimeout,
		WriteTimeout:      writeTimeout,
		IdleTimeout:       idleTimeout,
	}
}

func listen(srv *http.Server, cfg config.Config) {
	log.Printf("go-fetcher listening on %s (allowed hosts: %v)", cfg.Addr(), cfg.AllowedHosts)
	if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		log.Fatalf("server error: %v", err)
	}
}

func awaitTermination() {
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	<-stop
}

func shutdown(srv *http.Server) {
	ctx, cancel := context.WithTimeout(context.Background(), shutdownGrace)
	defer cancel()
	if err := srv.Shutdown(ctx); err != nil {
		log.Printf("graceful shutdown failed: %v", err)
	}
	log.Println("go-fetcher stopped")
}
