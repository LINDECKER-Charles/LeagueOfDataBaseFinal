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

	"lodb/go/api/internal/api"
	"lodb/go/api/internal/config"
	"lodb/go/api/internal/keys"
	"lodb/go/api/internal/metering"
	"lodb/go/api/internal/ratelimit"
	"lodb/go/api/internal/store"
	"lodb/go/api/internal/trends"
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

	recorder := metering.New(db, metering.Options{
		FlushInterval: config.MeterFlushInterval,
		FlushTimeout:  config.MeterFlushTimeout,
	}, log)
	meter := startMetering(recorder)

	wired := wiring{cfg: cfg, db: db, s3: s3, recorder: recorder, log: log}
	srv := newHTTPServer(cfg, wired.handler())
	go listen(srv, log)

	<-ctx.Done()
	return shutdown(srv, meter, log)
}

// wiring is what run assembled: the process-wide dependencies the HTTP surface
// is built from, as one value rather than an argument list whose order says
// nothing.
type wiring struct {
	cfg      config.Config
	db       *store.Postgres
	s3       *store.Minio
	recorder *metering.Recorder
	log      *slog.Logger
}

// handler assembles the HTTP surface over the stores.
func (w wiring) handler() http.Handler {
	names := trends.NewStoreNameResolver(w.s3, config.NamesCacheTTL, nil)
	return api.NewServer(api.Deps{
		Auth:    w.db,
		Content: w.db,
		Trends: trends.New(trends.Options{
			Reader:   w.s3,
			Names:    names,
			CacheTTL: config.TrendsCacheTTL,
			Log:      w.log,
		}),
		KeyCache:    keys.NewCache(config.KeyCacheTTL, nil),
		Limiter:     ratelimit.New(nil),
		Meter:       w.recorder,
		PGPing:      w.db,
		S3Ping:      w.s3,
		SiteBaseURL: w.cfg.PublicSiteURL,
		Log:         w.log,
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

func listen(srv *http.Server, log *slog.Logger) {
	log.Info("go-api listening", "addr", srv.Addr)
	if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		log.Error("server error", "error", err)
	}
}

// meterLoop is the handle on the metering goroutine: cancel it, then wait for
// its final flush.
type meterLoop struct {
	stop context.CancelFunc
	done <-chan struct{}
}

// startMetering runs the recorder on its own context, deliberately not the
// signal-cancelled one: the final flush still needs a live context.
func startMetering(recorder *metering.Recorder) meterLoop {
	ctx, cancel := context.WithCancel(context.Background())
	done := make(chan struct{})
	go func() { defer close(done); recorder.Run(ctx) }()
	return meterLoop{stop: cancel, done: done}
}

func (m meterLoop) stopAndDrain() {
	m.stop()
	<-m.done
}

// shutdown drains HTTP first, then lets the metering recorder do a final flush
// so buffered usage counters reach api_usage before exit.
func shutdown(srv *http.Server, meter meterLoop, log *slog.Logger) error {
	ctx, cancel := context.WithTimeout(context.Background(), config.ShutdownTimeout)
	defer cancel()
	err := srv.Shutdown(ctx)
	meter.stopAndDrain()
	log.Info("go-api stopped")
	return err
}
