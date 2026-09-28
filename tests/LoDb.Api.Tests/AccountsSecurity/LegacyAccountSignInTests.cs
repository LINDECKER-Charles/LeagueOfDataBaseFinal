using System.Globalization;
using System.Security.Claims;
using LoDb.Api.Modules.Accounts.Security.Policy;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// An account inserted as the legacy stack inserts it signs in: its bcrypt hash is rewritten
/// in argon2id, its null stamp is created, never an error. Five failures in a row lock it for
/// fifteen minutes, in <c>users</c>, so on every instance. A new account follows the policy.
/// </summary>
public sealed class LegacyAccountSignInTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string UserName = "Legende_42";
    private const string Ascii = "ascii";
    private const string StrongPassword = "Str0ng-passphrase!";
    private const int FailuresToLock = 5;

    private static readonly string Password = PhpHashes.Passwords[Ascii];
    private static readonly string WrongPassword = Password + "?";
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private TestDatabase? _database;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The database is not created yet.");

    [Fact]
    public async Task LegacyHashIsRewrittenInArgon2idAtTheFirstSignIn()
    {
        await InsertLegacyAccountAsync(PhpHashes.Hash("bcrypt", Ascii));
        await using var services = Services();

        var first = await SignInAsync(services, Password);
        var rewritten = await StoredAsync("password");
        var second = await SignInAsync(services, Password);

        Assert.Equal((true, true), (first.Succeeded, second.Succeeded));
        Assert.Matches(MigratingPasswordHasherTests.TargetFormat, rewritten);
        Assert.Equal(rewritten, await StoredAsync("password"));
    }

    [Fact]
    public async Task WrongPasswordKeepsTheHashAndCountsTheFailure()
    {
        var hash = PhpHashes.Hash("bcrypt-cost4", Ascii);
        await InsertLegacyAccountAsync(hash);
        await using var services = Services();

        var result = await SignInAsync(services, WrongPassword);

        Assert.Equal((false, false), (result.Succeeded, result.IsLockedOut));
        Assert.Equal(
            $"{hash}|1|NULL",
            await StoredAsync("concat_ws('|', password, access_failed_count, "
                + "coalesce(lockout_end::text, 'NULL'))"));
    }

    [Fact]
    public async Task FifthFailureLocksTheAccountForFifteenMinutesOnEveryInstance()
    {
        await InsertLegacyAccountAsync(PhpHashes.Hash("bcrypt-cost4", Ascii));
        await using var instance = Services();
        await using var otherInstance = Services();
        var lockedOut = new List<bool>();

        var before = TimeProvider.System.GetUtcNow();
        for (var failure = 0; failure < FailuresToLock; failure++)
        {
            lockedOut.Add((await SignInAsync(instance, WrongPassword)).IsLockedOut);
        }

        var after = TimeProvider.System.GetUtcNow();

        Assert.Equal([false, false, false, false, true], lockedOut);
        Assert.True((await SignInAsync(otherInstance, Password)).IsLockedOut);
        Assert.Equal(
            "0|t",
            await StoredAsync(
                $"concat_ws('|', access_failed_count, lockout_end BETWEEN "
                    + $"{Timestamp(before + LockoutDuration)} "
                    + $"AND {Timestamp(after + LockoutDuration)})"));
    }

    [Fact]
    public async Task NullStampIsCreatedAtSignIn()
    {
        await using var services = Services();
        var hash = services.GetRequiredService<IPasswordHasher<User>>()
            .HashPassword(new User { Roles = [] }, Password);
        await InsertLegacyAccountAsync(hash);
        await using var scope = services.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var user = await signIn.UserManager.FindByNameAsync(UserName);

        var result = await signIn.CheckPasswordSignInAsync(user!, Password, lockoutOnFailure: true);
        var principal = await signIn.CreateUserPrincipalAsync(user!);

        Assert.True(result.Succeeded);
        var stamp = principal.FindFirstValue(signIn.Options.ClaimsIdentity.SecurityStampClaimType);
        Assert.False(string.IsNullOrEmpty(stamp));
        Assert.Equal(stamp, await StoredAsync("security_stamp"));
    }

    [Fact]
    public async Task NewAccountFollowsThePolicyAndGetsAnArgon2idHash()
    {
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var weak = await users.CreateAsync(NewUser(), "password");
        var strong = await users.CreateAsync(NewUser(), StrongPassword);

        Assert.Equal(
            [
                CnilPasswordValidator.RuleLength, CnilPasswordValidator.RuleUppercase,
                CnilPasswordValidator.RuleDigit, CnilPasswordValidator.RuleSpecial,
                CnilPasswordValidator.TooCommon,
            ],
            weak.Errors.Select(static error => error.Code));
        Assert.True(strong.Succeeded);
        Assert.Matches(MigratingPasswordHasherTests.TargetFormat, await StoredAsync("password"));
    }

    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateDatabaseAsync(Cancellation);
        await _database.MigrateAsync(Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    private static User NewUser() => new()
    {
        UserName = "Nouveau_7",
        Email = "nouveau@example.test",
        Roles = ["ROLE_USER"],
        CreatedAt = TimeProvider.System.GetUtcNow(),
    };

    private static async Task<SignInResult> SignInAsync(ServiceProvider services, string password)
    {
        await using var scope = services.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var user = await signIn.UserManager.FindByNameAsync(UserName);
        return await signIn.CheckPasswordSignInAsync(user!, password, lockoutOnFailure: true);
    }

    // To the microsecond, as PostgreSQL stores it.
    private static string Timestamp(DateTimeOffset instant) =>
        $"'{instant.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffffzzz", CultureInfo.InvariantCulture)}'"
            + "::timestamptz";

    private ServiceProvider Services() =>
        AccountsSecurityRegistrationTests.Build(Database.ConnectionString);

    // A row as the legacy stack inserts it: its nullable columns left null, none of lot 4.
    private Task InsertLegacyAccountAsync(string hash) =>
        Database.ExecuteAsync(
            $"""
            INSERT INTO users (email, roles, password, username, is_public_profile,
                               is_supporter, created_at, is_banned, is_verified)
            VALUES ('legende@example.test', '["ROLE_USER"]', '{hash}', '{UserName}', false,
                    false, '2026-09-26 08:30:15', false, true)
            """,
            Cancellation);

    private async Task<string> StoredAsync(string expression) =>
        Assert.Single(await Database.QueryAsync($"SELECT {expression} FROM users", Cancellation));
}
