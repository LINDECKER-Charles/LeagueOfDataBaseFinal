-- Anonymizes a restored copy of the LoDb database, for next and for local trials (plan,
-- L1.4). No personal data survives; the structure and the row counts stay, except for the
-- two transient queues, which are emptied. anonymize.sh runs it in ONE transaction on a
-- throwaway copy: never run it on a database that serves.
--
-- Input: the setting lodb.anon_password_hash, the bcrypt hash every password becomes.
--
-- Guard: every column whose type can hold text is reviewed below, anonymized, emptied or
-- kept on purpose. A column added later (lots 4, 6, 7…) stops the script until its lot
-- reviews it here.

DO $guard$
DECLARE
    unreviewed text;
BEGIN
    IF coalesce(current_setting('lodb.anon_password_hash', true), '')
        !~ '^\$2[aby]\$[0-9]{2}\$[./A-Za-z0-9]{53}$' THEN
        RAISE EXCEPTION 'lodb.anon_password_hash must hold a bcrypt hash.';
    END IF;

    SELECT string_agg(format('%s.%s', c.relname, a.attname), ', '
                      ORDER BY c.relname, a.attname)
    INTO unreviewed
    FROM pg_class c
    JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
    WHERE c.relnamespace = current_schema()::regnamespace
      AND c.relkind IN ('r', 'p')
      AND NOT c.relispartition
      AND a.atttypid NOT IN (
          'smallint'::regtype, 'integer'::regtype, 'bigint'::regtype, 'numeric'::regtype,
          'real'::regtype, 'double precision'::regtype, 'boolean'::regtype, 'date'::regtype,
          'timestamp'::regtype, 'timestamptz'::regtype, 'time'::regtype,
          'timetz'::regtype, 'interval'::regtype)
      AND format('%s.%s', c.relname, a.attname) <> ALL (ARRAY[
          -- Anonymized below.
          'api_keys.key_hash', 'api_keys.key_prefix', 'api_keys.name',
          'api_keys.stripe_customer_id', 'api_keys.stripe_subscription_id',
          'builds.description', 'builds.name',
          'contact_messages.email', 'contact_messages.ip', 'contact_messages.message',
          'contact_messages.name', 'contact_messages.subject',
          'donations.stripe_session_id',
          'users.ban_reason', 'users.email', 'users.google_id', 'users.password',
          'users.riot_tagline', 'users.username',
          -- Emptied below: e-mails waiting to be sent, password reset tokens.
          'messenger_messages.body', 'messenger_messages.headers',
          'messenger_messages.queue_name',
          'reset_password_request.hashed_token', 'reset_password_request.selector',
          -- Kept: plans and enumerations, game data, settings, technical keys.
          'api_keys.plan',
          'builds.champion_id', 'builds.game_mode', 'builds.game_version',
          'builds.language', 'builds.runes', 'builds.share_token', 'builds.steps',
          'contact_messages.category', 'contact_messages.locale', 'contact_messages.status',
          'donations.currency',
          'users.favorite_champion_id', 'users.favorite_item_id', 'users.favorite_rune_id',
          'users.favorite_skin_id', 'users.favorite_summoner_id',
          'users.preferred_version', 'users.roles',
          'ddragon_asset.extension', 'ddragon_asset.key', 'ddragon_asset.sha256',
          'ddragon_asset.status', 'ddragon_asset.type', 'ddragon_asset.version',
          'ddragon_version.status', 'ddragon_version.version',
          'periodic_job.name',
          'doctrine_migration_versions.version',
          '__EFMigrationsHistory.migration_id', '__EFMigrationsHistory.product_version']);

    IF unreviewed IS NOT NULL THEN
        RAISE EXCEPTION 'Columns not reviewed by anonymize.sql: %.', unreviewed;
    END IF;
END
$guard$;

DELETE FROM messenger_messages;
DELETE FROM reset_password_request;

-- Two passes on the unique columns: a final value may equal the current value of another
-- row, which the unique indexes refuse even within one statement. No real username starts
-- with "~".
UPDATE users SET
    email = format('~%s~%s', id, md5(random()::text)),
    username = left(format('~%s~%s', id, md5(random()::text)), 24);

UPDATE users SET
    email = format('user%s@example.invalid', id),
    username = format('user%s', id),
    password = CASE WHEN password IS NOT NULL
        THEN current_setting('lodb.anon_password_hash') END,
    google_id = CASE WHEN google_id IS NOT NULL THEN format('anon-%s', id) END,
    riot_tagline = CASE WHEN riot_tagline IS NOT NULL THEN 'ANON' END,
    ban_reason = CASE WHEN ban_reason IS NOT NULL THEN 'Anonymized.' END;

UPDATE builds SET
    name = format('Build %s', id),
    description = CASE WHEN description IS NOT NULL
        THEN format('Description of build %s.', id) END;

-- The hash of an unknowable key: no key of the dump works anywhere. Stripe identifiers
-- become salted pseudonyms, so that two keys of one customer still share theirs (NULL
-- stays NULL through the concatenation).
UPDATE api_keys SET
    name = format('Key %s', id),
    key_hash = encode(sha256(convert_to(format('%s:%s', id, gen_random_uuid()), 'UTF8')), 'hex'),
    stripe_customer_id = 'cus_anon_' || left(md5(salt.value || stripe_customer_id), 24),
    stripe_subscription_id = 'sub_anon_' || left(md5(salt.value || stripe_subscription_id), 24)
FROM (SELECT gen_random_uuid()::text AS value) AS salt;

UPDATE api_keys SET key_prefix = 'lodb_' || left(key_hash, 7);

UPDATE donations SET stripe_session_id = format('cs_anon_%s', id);

-- 192.0.2.0/24 is reserved for documentation (RFC 5737).
UPDATE contact_messages SET
    name = CASE WHEN name IS NOT NULL THEN 'Anonymous' END,
    email = format('contact%s@example.invalid', id),
    subject = CASE WHEN subject IS NOT NULL THEN format('Subject %s', id) END,
    message = format('Message %s, anonymized.', id),
    ip = CASE WHEN ip IS NOT NULL THEN '192.0.2.1' END;
