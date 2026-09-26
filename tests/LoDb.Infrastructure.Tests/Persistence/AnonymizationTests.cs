using LoDb.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>tools/next/db/anonymize.sql</c> leaves no personal data, keeps the structure and the
/// row counts, empties the secrets, and stops on any text column it has not reviewed; with or
/// without the tables of the new stack.
/// </summary>
public sealed class AnonymizationTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    // The example of PHP's password_verify documentation: a valid bcrypt hash.
    private const string PasswordHash =
        "$2y$10$.vGA1O9wmRjrwAVXD98HNOgsNpDczlqm3Jq7KnEd1rVAGv3Fykk1a";

    // Neither generated values (hexadecimal, example.invalid, the hash above) nor kept ones
    // contain these: any match after the script is personal data left behind.
    private const string Marker = "zqx";
    private const string MarkedIp = "198.51.100.77";

    // Rows of the Doctrine tables, those of a dump taken before the new stack migrates.
    private const string LegacySeed = """
        INSERT INTO users (id, email, username, roles, password, created_at, google_id,
                           riot_tagline, ban_reason, favorite_champion_id, is_banned)
        VALUES
            (1, 'zqx.alice@zqxmail.test', 'zqxalice', '["ROLE_ADMIN"]', '$2y$13$zqxAlice',
             '2026-01-01 10:00:00', NULL, 'ZQX1', 'zqx spam', 'Ahri', true),
            (2, 'zqx.bob@zqxmail.test', 'zqxbob', '[]', NULL, '2026-01-02 10:00:00',
             'zqx-google-bob', NULL, NULL, NULL, false),
            -- Already holds user 1's final values: the unique indexes must not trip.
            (3, 'user1@example.invalid', 'user1', '[]', '$2y$13$zqxCarol',
             '2026-01-03 10:00:00', NULL, NULL, NULL, NULL, false);
        INSERT INTO builds (id, name, champion_id, game_version, description, runes, steps,
                            share_token, created_at, updated_at, owner_id)
        VALUES
            (1, 'zqx secret build', 'Ahri', '16.19.1', 'zqx private notes',
             '{"primary": 8100}', '[]', '0123456789abcdef01234567',
             '2026-01-01 10:00:00', '2026-01-01 10:00:00', 1),
            (2, 'zqx other build', 'Zed', '16.19.1', NULL, '{}', '[]',
             'fedcba9876543210fedcba98', '2026-01-02 10:00:00', '2026-01-02 10:00:00', 2);
        INSERT INTO build_votes (id, value, created_at, build_id, voter_id)
        VALUES (1, 1, '2026-01-03 10:00:00', 1, 2);
        INSERT INTO donations (id, stripe_session_id, amount_cents, currency, created_at, user_id)
        VALUES
            (1, 'cs_live_zqx001', 500, 'eur', '2026-01-04 10:00:00', 1),
            (2, 'cs_live_zqx002', 1000, 'usd', '2026-01-05 10:00:00', NULL);
        INSERT INTO api_keys (id, name, key_hash, key_prefix, plan, created_at,
                              stripe_customer_id, stripe_subscription_id, user_id)
        VALUES
            (1, 'zqx bot', 'zqx' || repeat('a', 61), 'lodb_zqx1', 'monthly', now(),
             'cus_zqxshared', 'sub_zqx1', 1),
            (2, 'zqx backup', 'zqx' || repeat('b', 61), 'lodb_zqx2', 'annual', now(),
             'cus_zqxshared', 'sub_zqx2', 1),
            (3, 'zqx trial', 'zqx' || repeat('c', 61), 'lodb_zqx3', 'free', now(),
             NULL, NULL, 2);
        INSERT INTO api_usage (id, day, requests, api_key_id) VALUES (1, '2026-09-25', 42, 1);
        INSERT INTO contact_messages (id, category, name, email, subject, message, locale, ip,
                                      status, created_at, handled_at, user_id)
        VALUES
            (1, 'bug', 'zqx sender', 'zqx.sender@zqxmail.test', 'zqx subject',
             'zqx message', 'fr', '198.51.100.77', 'new', '2026-01-06 10:00:00', NULL, 1),
            (2, 'feedback', NULL, 'zqx.anon@zqxmail.test', NULL, 'zqx other message', NULL,
             NULL, 'handled', '2026-01-07 10:00:00', '2026-01-08 10:00:00', NULL);
        INSERT INTO messenger_messages (id, body, headers, queue_name, created_at, available_at)
        VALUES (1, 'zqx body', 'zqx headers', 'zqx', now(), now());
        INSERT INTO reset_password_request (id, selector, hashed_token, requested_at,
                                            expires_at, user_id)
        VALUES (1, 'zqxsel', 'zqxtoken', now(), now(), 1);
        """;

    // Rows of the tables of the new stack: Data Dragon (lot 1), then Identity, the key ring,
    // the outbox and the journal (lot 4).
    private const string NewStackSeed = """
        INSERT INTO ddragon_version (version, status, discovered_at, updated_at)
        VALUES ('16.19.1', 'ready', now(), now());
        INSERT INTO ddragon_asset (version, type, key, status, recorded_at)
        VALUES ('16.19.1', 'champion', 'Hwei', 'absent', now());
        INSERT INTO periodic_job (name, last_started_at) VALUES ('ddragon-watch', now());
        UPDATE users SET security_stamp = 'ZQXSTAMP', two_factor_enabled = true,
            access_failed_count = 3, lockout_end = now(),
            concurrency_stamp = '5c0e2f4e-8f3b-4c1d-9a7e-2b6d1f0c3a91'
        WHERE id = 1;
        INSERT INTO identity_roles (id, name, normalized_name, concurrency_stamp)
        VALUES (1, 'Admin', 'ADMIN', '0b7d6c2a-1e4f-4a3b-8c9d-5e6f7a8b9c0d');
        INSERT INTO identity_user_roles (user_id, role_id) VALUES (1, 1);
        INSERT INTO identity_user_tokens (user_id, login_provider, name, value)
        VALUES (1, '[AspNetUserStore]', 'AuthenticatorKey', 'ZQXSECRET');
        INSERT INTO data_protection_keys (friendly_name, xml)
        VALUES ('key-zqx', '<key id="zqx" />');
        INSERT INTO email_outbox (recipient, template, locale, model, status, next_attempt_at,
                                  created_at)
        VALUES ('zqx.alice@zqxmail.test', 'confirm_email', 'fr', '{"userName": "zqxalice"}',
                'pending', now(), now());
        INSERT INTO audit_log (occurred_at, actor_type, actor_id, actor, action, outcome,
                               target_type, target_id, target, ip, route, meta)
        VALUES
            (now(), 'user', 1, 'zqxalice', 'build.create', 'success', 'build', '1',
             'zqx secret build', '198.51.100.77', '/api/builds', '{"champion": "Ahri"}'),
            (now(), 'anonymous', NULL, NULL, 'user.login_failed', 'failure', NULL, NULL, NULL,
             '198.51.100.77', '/api/account/login', '{"identifier": "zqx.bob@zqxmail.test"}'),
            (now(), 'admin', 3, 'zqxcarol', 'admin.user_ban', 'success', 'user', '2', 'zqxbob',
             NULL, '/api/admin/users/{id}/ban', NULL);
        """;

    private const string Seed = LegacySeed + "\n" + NewStackSeed;

    private static readonly string[] EmptiedTables =
    [
        "data_protection_keys", "email_outbox", "identity_user_tokens", "messenger_messages",
        "reset_password_request",
    ];

    [Fact]
    public async Task NoPersonalDataIsLeft()
    {
        await Database.ExecuteAsync(Seed, Cancellation);
        var marked = await MarkedRowsAsync();
        var counts = await RowCountsAsync();
        var catalog = await BaselineSchemaTests.ReadCatalogAsync(Database);

        await AnonymizeAsync(PasswordHash);

        Assert.Equal(20, marked.Values.Sum());
        Assert.All(await MarkedRowsAsync(), static table => Assert.Equal(0, table.Value));
        Assert.Equal(
            counts.ToDictionary(
                static table => table.Key,
                static table => EmptiedTables.Contains(table.Key) ? 0 : table.Value),
            await RowCountsAsync());
        Assert.Equal(catalog, await BaselineSchemaTests.ReadCatalogAsync(Database));
    }

    [Fact]
    public async Task AnonymizedValuesStayUsable()
    {
        await Database.ExecuteAsync(Seed, Cancellation);

        await AnonymizeAsync(PasswordHash);

        Assert.Equal(
            [
                $"user1|user1@example.invalid|{PasswordHash}|NULL|ANON|Anonymized.",
                "user2|user2@example.invalid|NULL|anon-2|NULL|NULL",
                $"user3|user3@example.invalid|{PasswordHash}|NULL|NULL|NULL",
            ],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', username, email, coalesce(password, 'NULL'),
                    coalesce(google_id, 'NULL'), coalesce(riot_tagline, 'NULL'),
                    coalesce(ban_reason, 'NULL'))
                FROM users ORDER BY id
                """,
                Cancellation));
        var keys = await Database.QueryAsync(
            """
            SELECT concat_ws('|', key_hash ~ '^[0-9a-f]{64}$',
                key_prefix = 'lodb_' || left(key_hash, 7),
                coalesce(stripe_customer_id, 'NULL'), coalesce(stripe_subscription_id, 'NULL'))
            FROM api_keys ORDER BY id
            """,
            Cancellation);
        var customers = keys.Select(static key => key.Split('|')[2]).ToList();
        Assert.All(keys, static key => Assert.StartsWith("t|t|", key, StringComparison.Ordinal));
        Assert.StartsWith("cus_anon_", customers[0], StringComparison.Ordinal);
        Assert.Equal(customers[0], customers[1]);
        Assert.Equal("NULL", customers[2]);
        Assert.Equal(
            ["1|sr|16.19.1|{\"primary\": 8100}|0123456789abcdef01234567"],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', id, game_mode, game_version, runes, share_token)
                FROM builds WHERE champion_id = 'Ahri'
                """,
                Cancellation));
    }

    [Fact]
    public async Task NewStackValuesStayUsable()
    {
        await Database.ExecuteAsync(Seed, Cancellation);

        await AnonymizeAsync(PasswordHash);

        Assert.Equal(
            ["USER1|USER1@EXAMPLE.INVALID|NULL|f|0|NULL|5c0e2f4e-8f3b-4c1d-9a7e-2b6d1f0c3a91"],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', normalized_username, normalized_email,
                    coalesce(security_stamp, 'NULL'), two_factor_enabled, access_failed_count,
                    coalesce(lockout_end::text, 'NULL'), concurrency_stamp)
                FROM users WHERE id = 1
                """,
                Cancellation));
        Assert.Equal(
            [
                "user|user1|build.create|Build 1|192.0.2.1|/api/builds|NULL",
                "anonymous|NULL|user.login_failed|NULL|192.0.2.1|/api/account/login|NULL",
                "admin|user3|admin.user_ban|user2|NULL|/api/admin/users/{id}/ban|NULL",
            ],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', actor_type, coalesce(actor, 'NULL'), action,
                    coalesce(target, 'NULL'), coalesce(ip, 'NULL'), route,
                    coalesce(meta::text, 'NULL'))
                FROM audit_log ORDER BY id
                """,
                Cancellation));
    }

    [Fact]
    public async Task DumpWithoutTheNewStackIsAnonymizedToo()
    {
        await using var legacy = await Server.CreateDatabaseAsync(Cancellation);
        await legacy.CreateDoctrineSchemaAsync(Cancellation);
        await legacy.ExecuteAsync(LegacySeed, Cancellation);
        var marked = await MarkedRowsAsync(legacy);

        await AnonymizeAsync(legacy, PasswordHash);

        Assert.DoesNotContain("audit_log", await legacy.TablesAsync(Cancellation));
        Assert.Equal(14, marked.Values.Sum());
        Assert.All(await MarkedRowsAsync(legacy), static table => Assert.Equal(0, table.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2y$10$tooShort")]
    public async Task MissingHashStopsBeforeAnyChange(string? passwordHash)
    {
        await Database.ExecuteAsync(Seed, Cancellation);
        var before = await MarkedRowsAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            AnonymizeAsync(passwordHash));

        Assert.Equal("lodb.anon_password_hash must hold a bcrypt hash.", error.MessageText);
        Assert.Equal(before, await MarkedRowsAsync());
    }

    [Theory]
    [InlineData("ALTER TABLE users ADD COLUMN nickname varchar(32)", "users.nickname")]
    [InlineData("ALTER TABLE builds ADD COLUMN notes jsonb", "builds.notes")]
    [InlineData("CREATE TABLE analytics_event (id integer, detail text)", "analytics_event.detail")]
    public async Task UnreviewedColumnStopsBeforeAnyChange(string alteration, string column)
    {
        await Database.ExecuteAsync(Seed, Cancellation);
        await Database.ExecuteAsync(alteration, Cancellation);
        var before = await MarkedRowsAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            AnonymizeAsync(PasswordHash));

        Assert.Equal($"Columns not reviewed by anonymize.sql: {column}.", error.MessageText);
        Assert.Equal(before, await MarkedRowsAsync());
    }

    [Fact]
    public async Task ColumnsThatHoldNoTextNeedNoReview()
    {
        await Database.ExecuteAsync(
            "ALTER TABLE users ADD COLUMN login_count integer, ADD COLUMN seen_at timestamptz",
            Cancellation);

        await AnonymizeAsync(PasswordHash);
    }

    private Task AnonymizeAsync(string? passwordHash) => AnonymizeAsync(Database, passwordHash);

    private static async Task AnonymizeAsync(TestDatabase database, string? passwordHash)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync(Cancellation);
        await using var transaction = await connection.BeginTransactionAsync(Cancellation);
        if (passwordHash is not null)
        {
            await using var setting = new NpgsqlCommand(
                "SELECT set_config('lodb.anon_password_hash', @hash, true)",
                connection,
                transaction);
            setting.Parameters.AddWithValue("hash", passwordHash);
            await setting.ExecuteNonQueryAsync(Cancellation);
        }

        await using var script = new NpgsqlCommand(
            LegacySchema.AnonymizeSql,
            connection,
            transaction);
        await script.ExecuteNonQueryAsync(Cancellation);
        await transaction.CommitAsync(Cancellation);
    }

    private Task<Dictionary<string, long>> MarkedRowsAsync() => MarkedRowsAsync(Database);

    // Per table, the rows whose text still holds a marker.
    private static Task<Dictionary<string, long>> MarkedRowsAsync(TestDatabase database) =>
        PerTableAsync(
            database,
            $"count(*) FILTER (WHERE t::text ILIKE '%{Marker}%' OR t::text LIKE '%{MarkedIp}%')");

    private Task<Dictionary<string, long>> RowCountsAsync() => PerTableAsync(Database, "count(*)");

    private static async Task<Dictionary<string, long>> PerTableAsync(
        TestDatabase database,
        string aggregate)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in await database.TablesAsync(Cancellation))
        {
            var value = await database.QueryAsync(
                $"SELECT {aggregate} FROM \"{table}\" AS t",
                Cancellation);
            result[table] = long.Parse(value[0], null);
        }

        return result;
    }
}
