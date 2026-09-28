using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Tests.Persistence.Accounts;

/// <summary>
/// <c>users</c> shared by both stacks: a row the legacy stack inserts reads as an Identity
/// user, PostgreSQL generates the normalized e-mail and username whoever writes the row, and
/// deleting an account deletes its grants and tokens.
/// </summary>
public sealed class UsersTableTests(PostgresContainerFixture postgres) : MigratedDatabase(postgres)
{
    /// <summary>The example of PHP's password_hash documentation, for "rasmuslerdorf".</summary>
    internal const string LegacyHash =
        "$2y$10$.vGA1O9wmRjrwAVXD98HNOgsNpDczlqm3Jq7KnEd1rVAGv3Fykk1a";

    /// <summary>
    /// The insert of the legacy stack: every column the Doctrine entity maps, none of lot 4.
    /// </summary>
    internal const string LegacyInsert = $"""
        INSERT INTO users (email, roles, password, username, google_id, riot_tagline,
                           is_public_profile, is_supporter, created_at, is_banned, banned_at,
                           ban_reason, is_verified, favorite_champion_id, favorite_item_id,
                           favorite_rune_id, favorite_summoner_id, favorite_skin_id,
                           preferred_version)
        VALUES ('legende@example.test', '["ROLE_USER"]', '{LegacyHash}', 'Legende_42', NULL,
                NULL, false, false, '2026-09-26 08:30:15', false, NULL, NULL, true, NULL,
                NULL, NULL, NULL, NULL, NULL)
        """;

    [Fact]
    public async Task RowOfTheLegacyStackReadsAsAnIdentityUser()
    {
        await Database.ExecuteAsync(LegacyInsert, Cancellation);

        await using var context = Database.CreateContext();
        var user = await context.Users.AsNoTracking().SingleAsync(Cancellation);

        Assert.Equal(
            ("legende@example.test", "Legende_42", LegacyHash, true),
            (user.Email, user.UserName, user.PasswordHash, user.EmailConfirmed));
        Assert.Equal(
            ("LEGENDE@EXAMPLE.TEST", "LEGENDE_42"),
            (user.NormalizedEmail, user.NormalizedUserName));
        Assert.Equal(
            ((string?)null, (string?)null, (DateTimeOffset?)null),
            (user.SecurityStamp, user.ConcurrencyStamp, user.LockoutEnd));
        Assert.Equal(
            (true, 0, false),
            (user.LockoutEnabled, user.AccessFailedCount, user.TwoFactorEnabled));
    }

    [Fact]
    public async Task NormalizedColumnsAreStoredGeneratedColumns() =>
        Assert.Equal(
            [
                "normalized_email s upper((email)::text)",
                "normalized_username s upper((username)::text)",
            ],
            await Database.QueryAsync(
                """
                SELECT concat_ws(' ', a.attname, a.attgenerated, pg_get_expr(d.adbin, d.adrelid))
                FROM pg_attribute a
                JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                WHERE a.attrelid = 'users'::regclass AND a.attname LIKE 'normalized%'
                ORDER BY a.attname
                """,
                Cancellation));

    [Fact]
    public async Task NormalizedValuesWrittenByTheModelAreIgnored()
    {
        var user = new User
        {
            Email = "mixed@example.test",
            UserName = "MiXeD",
            Roles = [],
            CreatedAt = new DateTimeOffset(2026, 9, 26, 8, 30, 15, TimeSpan.Zero),
            NormalizedEmail = "WRONG",
            NormalizedUserName = "WRONG",
        };

        await using (var context = Database.CreateContext())
        {
            context.Add(user);
            await context.SaveChangesAsync(Cancellation);
            user.Email = "renamed@example.test";
            user.NormalizedEmail = "STILL WRONG";
            await context.SaveChangesAsync(Cancellation);
        }

        Assert.Equal(
            ("RENAMED@EXAMPLE.TEST", "MIXED"),
            (user.NormalizedEmail, user.NormalizedUserName));
        Assert.Equal(
            ["RENAMED@EXAMPLE.TEST|MIXED"],
            await Database.QueryAsync(
                "SELECT normalized_email || '|' || normalized_username FROM users",
                Cancellation));
    }

    // The legacy validators accept ASCII only (html5 e-mails, the username pattern); accented
    // letters agree too. PostgreSQL's upper() differs on a few others, such as "ß".
    [Theory]
    [InlineData("player.one+lol@example.test", "Legende_42")]
    [InlineData("éloïse@exämple.test", "Zoé.Ärger")]
    public async Task GeneratedValuesMatchTheNormalizerOfIdentity(string email, string userName)
    {
        await using (var context = Database.CreateContext())
        {
            context.Add(new User
            {
                Email = email,
                UserName = userName,
                Roles = [],
                CreatedAt = new DateTimeOffset(2026, 9, 26, 8, 30, 15, TimeSpan.Zero),
            });
            await context.SaveChangesAsync(Cancellation);
        }

        var normalizer = new UpperInvariantLookupNormalizer();
        Assert.Equal(
            [$"{normalizer.NormalizeEmail(email)}|{normalizer.NormalizeName(userName)}"],
            await Database.QueryAsync(
                "SELECT normalized_email || '|' || normalized_username FROM users",
                Cancellation));
    }

    [Fact]
    public async Task DeletingAnAccountDeletesItsGrantsAndTokens()
    {
        await Database.ExecuteAsync(LegacyInsert, Cancellation);
        await Database.ExecuteAsync(
            """
            INSERT INTO identity_roles (name, normalized_name) VALUES ('Admin', 'ADMIN');
            INSERT INTO identity_user_roles (user_id, role_id)
            SELECT u.id, r.id FROM users u CROSS JOIN identity_roles r;
            INSERT INTO identity_user_tokens (user_id, login_provider, name, value)
            SELECT id, '[AspNetUserStore]', 'AuthenticatorKey', 'JBSWY3DPEHPK3PXP' FROM users;
            """,
            Cancellation);

        // As the legacy stack deletes an account, knowing none of these tables.
        await Database.ExecuteAsync("DELETE FROM users", Cancellation);

        Assert.Equal(
            ["1|0|0"],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', (SELECT count(*) FROM identity_roles),
                    (SELECT count(*) FROM identity_user_roles),
                    (SELECT count(*) FROM identity_user_tokens))
                """,
                Cancellation));
    }
}
