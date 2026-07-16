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

	"go-workers/internal/api"
	"go-workers/internal/config"
	"go-workers/internal/fetcher"
)

func main() {
	cfg := config.Load()

	// Idle pool sized to the batch concurrency: DDragon is HTTP/1.1, so each
	// concurrent fetch needs its own reusable keep-alive connection.
	f := fetcher.New(cfg.AllowedHosts, cfg.RequestTimeout, cfg.MaxConcurrency)
	srv := &http.Server{
		Addr:              cfg.Addr(),
		Handler:           api.NewServer(f, cfg.MaxConcurrency, cfg.MaxURLsPerRequest),
		ReadHeaderTimeout: 5 * time.Second,
		WriteTimeout:      60 * time.Second,
		IdleTimeout:       90 * time.Second,
	}

	go func() {
		log.Printf("go-fetcher listening on %s (allowed hosts: %v)", cfg.Addr(), cfg.AllowedHosts)
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			log.Fatalf("server error: %v", err)
		}
	}()

	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	<-stop

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	if err := srv.Shutdown(ctx); err != nil {
		log.Printf("graceful shutdown failed: %v", err)
	}
	log.Println("go-fetcher stopped")
}
