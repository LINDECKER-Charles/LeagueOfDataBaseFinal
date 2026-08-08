// Package store contains the persistence adapters of go-api: Postgres for
// user-facing data and metering, MinIO for analytics aggregates and datasets.
package store

import (
	"context"
	"errors"
	"time"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
)

// ErrNotFound is the store-level sentinel for a missing row.
var ErrNotFound = errors.New("not found")

const pingTimeout = 2 * time.Second

// Postgres wraps the pgx pool with the query surface go-api needs.
type Postgres struct {
	pool *pgxpool.Pool
}

// NewPostgres connects a lazy pool (no round-trip at construction so the API
// can start while the database is still warming up).
func NewPostgres(ctx context.Context, databaseURL string) (*Postgres, error) {
	cfg, err := pgxpool.ParseConfig(databaseURL)
	if err != nil {
		return nil, err
	}
	pool, err := pgxpool.NewWithConfig(ctx, cfg)
	if err != nil {
		return nil, err
	}
	return &Postgres{pool: pool}, nil
}

// Close releases the pool.
func (p *Postgres) Close() { p.pool.Close() }

// Ping reports basic connectivity (used by /healthz only — schema presence is
// intentionally not checked so healthz stays green before migrations run).
func (p *Postgres) Ping(ctx context.Context) error {
	ctx, cancel := context.WithTimeout(ctx, pingTimeout)
	defer cancel()
	return p.pool.Ping(ctx)
}

// AddUsage upserts the day's request counters for a batch of keys. Implements
// metering.UsageWriter.
func (p *Postgres) AddUsage(ctx context.Context, requestsByKey map[int]int64) error {
	batch := &pgx.Batch{}
	for keyID, requests := range requestsByKey {
		batch.Queue(
			`INSERT INTO api_usage (api_key_id, day, requests)
			 VALUES ($1, CURRENT_DATE, $2)
			 ON CONFLICT (api_key_id, day)
			 DO UPDATE SET requests = api_usage.requests + EXCLUDED.requests`,
			keyID, requests,
		)
	}
	return p.pool.SendBatch(ctx, batch).Close()
}
