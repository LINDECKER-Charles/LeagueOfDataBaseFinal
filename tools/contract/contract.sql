-- Contract phase of the cutover (docs/reecriture/bascule.md, "Contract"; README.md here).
--
-- PREPARED, NOT APPLIED. It sits outside src/LoDb.Infrastructure/Persistence/Migrations on
-- purpose: `migrate` never sees it. It is applied once, thirty days after the cutover
-- without a rollback, as the body of an EF migration (README.md, "Apply"). From then on the
-- legacy stack can no longer run on the database: the rollback is over.
--
-- Plain SQL without psql meta-commands and without BEGIN/COMMIT: EF runs a migration in
-- its own transaction, and the check runs it with `psql --single-transaction`.
-- Idempotent: a second run changes nothing.

-- A table rewrite (the type changes below) takes an exclusive lock: wait for it briefly,
-- never queue the whole site behind it.
SET LOCAL lock_timeout = '5s';

-- ── Guard: only a database at the rewrite's schema ────────────────────────────────────
DO $guard$
DECLARE
  migrated boolean := false;
BEGIN
  -- Read dynamically: a static query on a missing table fails before the test.
  IF to_regclass('"__EFMigrationsHistory"') IS NOT NULL THEN
    EXECUTE 'SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = $1)'
      INTO migrated USING '20260926185409_Lot6BillingAnalyticsApps';
  END IF;
  IF NOT migrated THEN
    RAISE EXCEPTION 'contract: the EF history lacks 20260926185409_Lot6BillingAnalyticsApps;'
      ' run migrate first, never contract a database the rewrite has not migrated';
  END IF;
END
$guard$;

-- ── 1. Tables only the legacy stack used ──────────────────────────────────────────────
-- Symfony Messenger's queue (e-mails of the legacy stack, never sent again), the reset
-- tokens of SymfonyCasts' bundle (the rewrite's tokens are Identity's), and Doctrine's
-- migration history (the EF history keeps Baseline).
DROP TABLE IF EXISTS messenger_messages;
DROP TABLE IF EXISTS reset_password_request;
DROP TABLE IF EXISTS doctrine_migration_versions;

-- ── 2. Dead columns ───────────────────────────────────────────────────────────────────
-- Symfony roles, a JSON array only the legacy stack read; the rewrite's roles live in
-- identity_user_roles.
ALTER TABLE users DROP COLUMN IF EXISTS roles;

-- ── 3. Legacy timestamps to timestamptz (UTC) ─────────────────────────────────────────
-- The legacy stack wrote UTC (date.timezone = UTC) into timestamp(0) without time zone;
-- the rewrite reads them through LegacyUtcTimestamps.Converter. Converted explicitly AT
-- TIME ZONE 'UTC', never through the session's TimeZone. Precision 0 stays, like the
-- legacy columns already in timestamptz (api_keys.created_at, users.banned_at).
DO $types$
DECLARE
  target record;
BEGIN
  FOR target IN
    SELECT c.table_name, c.column_name, c.column_default IS NOT NULL AS has_default
    FROM information_schema.columns AS c
    JOIN (VALUES
      ('build_votes', 'created_at'),
      ('builds', 'created_at'),
      ('builds', 'updated_at'),
      ('contact_messages', 'created_at'),
      ('contact_messages', 'handled_at'),
      ('donations', 'created_at'),
      ('users', 'created_at')
    ) AS legacy (table_name, column_name)
      ON legacy.table_name = c.table_name AND legacy.column_name = c.column_name
    WHERE c.table_schema = current_schema()
      AND c.data_type = 'timestamp without time zone'
  LOOP
    -- The default (DEFAULT NULL::timestamp) is typed: dropped, then declared again.
    EXECUTE format(
      'ALTER TABLE %1$I ALTER COLUMN %2$I DROP DEFAULT, '
        || 'ALTER COLUMN %2$I TYPE timestamp(0) with time zone USING %2$I AT TIME ZONE ''UTC''',
      target.table_name, target.column_name);
    IF target.has_default THEN
      EXECUTE format('ALTER TABLE %I ALTER COLUMN %I SET DEFAULT NULL',
        target.table_name, target.column_name);
    END IF;
  END LOOP;
END
$types$;
