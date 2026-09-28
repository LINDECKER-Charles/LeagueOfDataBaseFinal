-- Data set of the /v1 contract references (L6.1), loaded into an EMPTY copy of the
-- old-stack schema. Everything is fixed except api_usage days, which are relative to
-- CURRENT_DATE so quotas are evaluated on the month of the capture.
-- Raw keys live in keys.json; only their SHA-256 is stored, as go-api expects.

-- Profiles read by /v1/profiles/{username} (favorites, visibility, ban).
INSERT INTO users (id, email, username, roles, is_public_profile, favorite_champion_id,
                   favorite_item_id, favorite_rune_id, favorite_summoner_id, created_at,
                   is_banned, banned_at, ban_reason, is_verified)
VALUES
  (1, 'public@v1-capture.test', 'PublicPlayer', '[]', true, 'Ahri', '3006', '8112',
   'SummonerFlash', '2026-07-01 09:12:00', false, NULL, NULL, true),
  (2, 'private@v1-capture.test', 'PrivatePlayer', '[]', false, 'Zed', NULL, NULL, NULL,
   '2026-07-02 10:00:00', false, NULL, NULL, true),
  (3, 'banned@v1-capture.test', 'BannedPlayer', '[]', true, 'Teemo', NULL, NULL, NULL,
   '2026-07-03 11:00:00', true, '2026-07-10 08:00:00+00', 'spam', true),
  (4, 'nofavorites@v1-capture.test', 'NoFavorites', '[]', true, NULL, NULL, NULL, NULL,
   '2026-07-04 12:00:00', false, NULL, NULL, true);

-- One owner per API key (the site allows a single active key per account).
INSERT INTO users (id, email, username, roles, created_at, is_verified)
SELECT 10 + n, format('client%s@v1-capture.test', n), format('ApiClient%s', n), '[]',
       '2026-07-01 00:00:00', true
  FROM generate_series(0, 15) AS n;

-- 55 public Aatrox builds of PublicPlayer. Pairs share created_at so the id DESC
-- tie-break is observable; every fifth build has no description.
INSERT INTO builds (id, name, champion_id, game_version, description, runes, steps,
                    is_public, share_token, created_at, updated_at, owner_id)
SELECT 100 + n, format('Aatrox build %s', lpad(n::text, 2, '0')), 'Aatrox', '16.18.1',
       CASE WHEN n % 5 = 0 THEN NULL ELSE format('Guide number %s.', n) END,
       '{"primary": {"style": 8000, "keystone": 8010}, "secondary": {"style": 8400}}',
       '[{"label": "Core", "items": ["3074", "6630"]}]',
       true, format('aatroxpub%s', lpad(n::text, 3, '0')),
       timestamp '2026-07-01 00:00:00' + (n / 2) * interval '1 hour',
       timestamp '2026-07-01 00:00:00' + (n / 2) * interval '1 hour', 1
  FROM generate_series(1, 55) AS n;

-- Private Aatrox builds, newer than every public one: never listed nor counted.
INSERT INTO builds (id, name, champion_id, game_version, description, runes, steps,
                    is_public, share_token, created_at, updated_at, owner_id)
SELECT 200 + n, format('Aatrox private %s', n), 'Aatrox', '16.18.1', NULL, '{}', '[]',
       false, format('aatroxpriv%s', n), timestamp '2026-09-01 00:00:00',
       timestamp '2026-09-01 00:00:00', 1
  FROM generate_series(0, 2) AS n;

-- Public builds of other owners: a banned account and a private profile.
INSERT INTO builds (id, name, champion_id, game_version, description, runes, steps,
                    is_public, share_token, created_at, updated_at, owner_id)
VALUES
  (210, 'Banned Aatrox A', 'Aatrox', '16.17.1', 'Posted before the ban.', '{}', '[]', true,
   'bannedaatroxa', '2026-08-01 10:00:00', '2026-08-01 10:00:00', 3),
  (211, 'Banned Aatrox B', 'Aatrox', '16.17.1', NULL, '{}', '[]', true,
   'bannedaatroxb', '2026-08-02 10:00:00', '2026-08-02 10:00:00', 3),
  (300, 'Ahri mid burst', 'Ahri', '16.18.1',
   '<b>Burst</b> & "roam" — combo E→Q, 100% <script>safe</script>',
   '{"shards": [5008, 5008, 5001], "primary": {"style": 8100, "keystone": 8112, "perks": [8139, 8138, 8135]}, "secondary": {"style": 8300, "perks": [8313, 8345]}}',
   '[{"label": "Start", "items": ["1056", "2003", "2003"]}, {"label": "Core", "items": ["6655", "3020", "4645"], "note": null}, {"label": "Empty", "items": []}]',
   true, 'ahrimidburst', '2026-07-20 18:40:00', '2026-07-21 09:00:00', 1),
  (301, 'Ahri support', 'Ahri', '15.24.1', NULL, '{}', '[]', true, 'ahrisupport',
   '2026-07-19 08:00:00', '2026-07-19 08:00:00', 1),
  (302, 'Ahri private', 'Ahri', '16.18.1', NULL, '{}', '[]', false, 'ahriprivate',
   '2026-07-25 08:00:00', '2026-07-25 08:00:00', 1),
  (400, 'Ahri from a private profile', 'Ahri', '16.18.1', NULL, '{}', '[]', true,
   'ahriprivateowner', '2026-07-18 08:00:00', '2026-07-18 08:00:00', 2);

-- One key per plan, then the keys dedicated to a single behaviour.
INSERT INTO api_keys (id, user_id, name, key_hash, key_prefix, plan, monthly_quota,
                      credits_balance, rate_limit_per_min, is_active, created_at, revoked_at)
SELECT k.id, k.user_id, k.name, encode(sha256(convert_to(k.raw, 'UTF8')), 'hex'),
       substr(k.raw, 1, 12), k.plan, k.quota, k.credits, k.rate, k.active,
       timestamptz '2026-07-01 00:00:00+00', k.revoked_at
  FROM (VALUES
    (1, 10, 'free', 'lodb_7a6835c3c34394b61bc291aa6f0029a8da1b9983',
     'free', 500, 0, 10, true, NULL::timestamptz),
    (2, 11, 'credits', 'lodb_926c7f2bd824ee97f7e1dc8fa56863b5b9633f40',
     'credits', 500, 5000, 60, true, NULL),
    (3, 12, 'monthly', 'lodb_44c474aa7cbe09a36ce12501971d9b71565706e5',
     'monthly', 15000, 0, 120, true, NULL),
    (4, 13, 'monthly_plus', 'lodb_4f8c091149898ec90c0a64643840af0cbc27a664',
     'monthly_plus', 45000, 0, 120, true, NULL),
    (5, 14, 'annual', 'lodb_c8241cc9d3e77d45febe46700796bef87721f92b',
     'annual', 20000, 0, 300, true, NULL),
    (6, 15, 'annual_plus', 'lodb_ad1024994ed559c60f94bb9452dd3e25087ecb1d',
     'annual_plus', 60000, 0, 300, true, NULL),
    (7, 16, 'revoked', 'lodb_e5ee0102a58efd37b67ef3f4701ad9912d057965',
     'monthly', 15000, 0, 120, false, timestamptz '2026-07-20 10:00:00+00'),
    (8, 17, 'inactive', 'lodb_92bbd45f4999281f0dda604e75888875f308183b',
     'free', 500, 0, 10, false, NULL),
    (9, 18, 'quota_edge', 'lodb_2ca3c551d5d510261bc2aec8997aa3691c86cdff',
     'free', 500, 2, 60, true, NULL),
    (10, 19, 'quota_exhausted', 'lodb_ae3a5b818cc5c3ca0717a998a9d06619b59c82d8',
     'free', 500, 0, 10, true, NULL),
    (11, 20, 'burst', 'lodb_888259fa700538c7b878c961b3a8a590382ca18b',
     'free', 500, 0, 10, true, NULL),
    (12, 21, 'billing', 'lodb_77cf19eadba147aa83013d52d72d9d2bd2bf050c',
     'monthly', 15000, 0, 120, true, NULL),
    (13, 22, 'revocation_target', 'lodb_474ffe4fc4a4205c752b7327359df917d7c5586e',
     'free', 500, 0, 10, true, NULL),
    (14, 23, 'top_up', 'lodb_06e5edac2a35a99490a5c4d72b642e99fb2eb912',
     'free', 500, 0, 10, true, NULL),
    (15, 24, 'previous_month', 'lodb_f02cafc60962c5cffa3ee75936820091108f912c',
     'free', 500, 0, 10, true, NULL)
  ) AS k (id, user_id, name, raw, plan, quota, credits, rate, active, revoked_at);

-- Month-to-date consumption, plus rows of the previous month that must not count.
INSERT INTO api_usage (api_key_id, day, requests)
VALUES
  (3, CURRENT_DATE, 137),
  (3, (date_trunc('month', CURRENT_DATE) - interval '1 day')::date, 1000),
  (9, CURRENT_DATE, 499),
  (10, CURRENT_DATE, 500),
  (14, CURRENT_DATE, 500),
  (15, (date_trunc('month', CURRENT_DATE) - interval '1 day')::date, 500);
