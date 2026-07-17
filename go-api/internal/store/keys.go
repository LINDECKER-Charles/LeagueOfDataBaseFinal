package store

import (
	"context"
	"errors"

	"github.com/jackc/pgx/v5"

	"leagueofdatabase/go-api/internal/keys"
)

// KeyByHash resolves an API key row by the SHA-256 hex of the raw key.
func (p *Postgres) KeyByHash(ctx context.Context, hash string) (keys.APIKey, error) {
	var k keys.APIKey
	err := p.pool.QueryRow(ctx,
		`SELECT id, user_id, plan, monthly_quota, credits_balance,
		        rate_limit_per_min, is_active, revoked_at
		   FROM api_keys WHERE key_hash = $1`, hash,
	).Scan(&k.ID, &k.UserID, &k.Plan, &k.MonthlyQuota, &k.CreditsBalance,
		&k.RateLimitPerMin, &k.Active, &k.RevokedAt)
	if errors.Is(err, pgx.ErrNoRows) {
		return keys.APIKey{}, ErrNotFound
	}
	return k, err
}

// MonthlyUsage sums the key's requests for the current calendar month.
func (p *Postgres) MonthlyUsage(ctx context.Context, keyID int) (int64, error) {
	var used int64
	err := p.pool.QueryRow(ctx,
		`SELECT COALESCE(SUM(requests), 0) FROM api_usage
		  WHERE api_key_id = $1 AND day >= date_trunc('month', CURRENT_DATE)`, keyID,
	).Scan(&used)
	return used, err
}

// UsageSnapshot is the live billing state reported by /v1/usage.
type UsageSnapshot struct {
	Plan            string
	MonthlyQuota    int64
	CreditsBalance  int64
	RateLimitPerMin int
	UsedThisMonth   int64
}

// UsageSnapshot reads the key's plan state and month-to-date usage in one
// query. Fresh from the database on purpose (billing endpoint) — it only lags
// by the metering flush interval (~1 s of unflushed requests).
func (p *Postgres) UsageSnapshot(ctx context.Context, keyID int) (UsageSnapshot, error) {
	var s UsageSnapshot
	err := p.pool.QueryRow(ctx,
		`SELECT k.plan, k.monthly_quota, k.credits_balance, k.rate_limit_per_min,
		        COALESCE((SELECT SUM(u.requests) FROM api_usage u
		                   WHERE u.api_key_id = k.id
		                     AND u.day >= date_trunc('month', CURRENT_DATE)), 0)
		   FROM api_keys k WHERE k.id = $1`, keyID,
	).Scan(&s.Plan, &s.MonthlyQuota, &s.CreditsBalance, &s.RateLimitPerMin, &s.UsedThisMonth)
	if errors.Is(err, pgx.ErrNoRows) {
		return UsageSnapshot{}, ErrNotFound
	}
	return s, err
}

// ConsumeCredit atomically spends one prepaid credit. The WHERE guard makes
// concurrent spends safe: whoever decrements last simply finds no row and the
// request is denied.
func (p *Postgres) ConsumeCredit(ctx context.Context, keyID int) (balance int64, spent bool, err error) {
	err = p.pool.QueryRow(ctx,
		`UPDATE api_keys SET credits_balance = credits_balance - 1
		  WHERE id = $1 AND credits_balance > 0
		  RETURNING credits_balance`, keyID,
	).Scan(&balance)
	if errors.Is(err, pgx.ErrNoRows) {
		return 0, false, nil
	}
	return balance, err == nil, err
}
