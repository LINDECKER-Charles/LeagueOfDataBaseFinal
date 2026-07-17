// go-api is the public paid REST API of LeagueOfDataBase: key-authenticated
// read access to public profiles, champion builds and consultation trends.
package main

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"

	"leagueofdatabase/go-api/internal/api"
	"leagueofdatabase/go-api/internal/config"
	"leagueofdatabase/go-api/internal/keys"
	"leagueofdatabase/go-api/internal/metering"
	"leagueofdatabase/go-api/internal/ratelimit"
	"leagueofdatabase/go-api/internal/store"
	"leagueofdatabase/go-api/internal/trends"
)

func main() {
	log := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	cfg := config.Load()
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	if err := run(ctx, cfg, log); err != nil {
		log.Error("fatal", "error", err)
		os.Exit(1)
	}
}

// run wires dependencies and blocks until shutdown completes.
func run(ctx context.Context, cfg config.Config, log *slog.Logger) error {
	db, err := store.NewPostgres(ctx, cfg.DatabaseURL)
	if err != nil {
		return err
	}
	defer db.Close()
	s3, err := store.NewMinio(cfg)
	if err != nil {
		return err
	}

	recorder := metering.New(db, config.MeterFlushInterval, log)
	meterCtx, stopMeter := context.WithCancel(context.Background())
	meterDone := make(chan struct{})
	go func() { defer close(meterDone); recorder.Run(meterCtx) }()

	srv := newHTTPServer(cfg, buildHandler(cfg, db, s3, recorder, log))
	go func() {
		log.Info("go-api listening", "addr", cfg.Addr())
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			log.Error("server error", "error", err)
		}
	}()

	<-ctx.Done()
	return shutdown(srv, stopMeter, meterDone, log)
}

// buildHandler assembles the HTTP surface over the stores.
func buildHandler(cfg config.Config, db *store.Postgres, s3 *store.Minio,
	recorder *metering.Recorder, log *slog.Logger) http.Handler {
	names := trends.NewStoreNameResolver(s3, config.NamesCacheTTL, nil)
	return api.NewServer(api.Deps{
		Auth:     db,
		Content:  db,
		Trends:   trends.New(s3, names, config.TrendsCacheTTL, nil),
		KeyCache: keys.NewCache(config.KeyCacheTTL, nil),
		Limiter:  ratelimit.New(nil),
		Meter:    recorder,
		PGPing:   db,
		S3Ping:   s3,
		Log:      log,
	})
}

func newHTTPServer(cfg config.Config, handler http.Handler) *http.Server {
	return &http.Server{
		Addr:              cfg.Addr(),
		Handler:           handler,
		ReadHeaderTimeout: config.ReadHeaderTimeout,
		WriteTimeout:      config.WriteTimeout,
		IdleTimeout:       config.IdleTimeout,
	}
}

// shutdown drains HTTP first, then lets the metering recorder do a final flush
// so buffered usage counters reach api_usage before exit.
func shutdown(srv *http.Server, stopMeter context.CancelFunc, meterDone <-chan struct{}, log *slog.Logger) error {
	ctx, cancel := context.WithTimeout(context.Background(), config.ShutdownTimeout)
	defer cancel()
	err := srv.Shutdown(ctx)
	stopMeter()
	<-meterDone
	log.Info("go-api stopped")
	return err
}
