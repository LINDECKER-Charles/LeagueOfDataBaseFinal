-- Reference schema for the go-api service (NOT applied by go-api itself).
-- The authoritative migration is a Doctrine migration in app/migrations/ owned
-- by the Symfony side; this file documents the contract go-api consumes and is
-- used to bootstrap throwaway databases in integration tests.

CREATE TABLE api_keys (
  id SERIAL PRIMARY KEY,
  user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  name VARCHAR(64) NOT NULL,
  key_hash VARCHAR(64) NOT NULL UNIQUE,          -- SHA-256 hex of the full raw key
  key_prefix VARCHAR(12) NOT NULL,               -- displayable prefix (e.g. lodb_ab12)
  plan VARCHAR(16) NOT NULL DEFAULT 'free',
  monthly_quota INT NOT NULL DEFAULT 500,
  credits_balance BIGINT NOT NULL DEFAULT 0,     -- prepaid requests, spent after quota
  rate_limit_per_min INT NOT NULL DEFAULT 10,
  is_active BOOLEAN NOT NULL DEFAULT true,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  revoked_at TIMESTAMPTZ
);

CREATE TABLE api_usage (
  id BIGSERIAL PRIMARY KEY,
  api_key_id INT NOT NULL REFERENCES api_keys(id) ON DELETE CASCADE,
  day DATE NOT NULL,
  requests BIGINT NOT NULL DEFAULT 0,
  UNIQUE (api_key_id, day)
);

-- Read-only tables consumed by go-api (owned by app/migrations/Version20260717101320.php):
--   users  (id, username, is_public_profile, favorite_champion_id, favorite_item_id,
--           favorite_rune_id, favorite_summoner_id, created_at)
--   builds (id, owner_id, name, champion_id, game_version, description,
--           runes JSONB, steps JSONB, is_public, share_token, created_at)
