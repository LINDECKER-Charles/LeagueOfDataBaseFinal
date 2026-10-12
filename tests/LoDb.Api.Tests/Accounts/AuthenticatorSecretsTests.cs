using System.Net;
using LoDb.Api.Modules.Accounts.Security.Authenticator;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The secrets of the second factor at rest: the authenticator key sealed, the recovery
/// codes hashed and spent once, each code of the app accepted once, and the secrets an
/// earlier version left in clear protected without locking anyone out.
/// </summary>
/// <remarks>
/// The tests share a host, each with an account of its own; only one leaves its secrets in
/// clear, so the upkeep finds that account alone to protect.
/// </remarks>
public sealed class AuthenticatorSecretsTests(SharedAccountsApp host)
    : IClassFixture<SharedAccountsApp>
{
    private const string ClearKey = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private const string ClearCodes = "BCDFG-HJKMN;PQRTV-WXY23;45678-9BCDF";
    private const string HashPrefix = "$argon2id$";
    private static readonly TimeSpan NextStep = TimeSpan.FromSeconds(30);

    private readonly AccountSeed _account = host.Unique();

    private AccountsApp App { get; } = host.App;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task KeyAndRecoveryCodesAreNotStoredInClear()
    {
        await App.SeedAsync(_account);

        var secrets = await App.EnableTwoFactorAsync(_account.Username);

        var key = await StoredAsync(IdentityTokenNames.AuthenticatorKey);
        var codes = await StoredAsync(IdentityTokenNames.RecoveryCodes);
        Assert.StartsWith(AuthenticatorKeyProtection.SealedPrefix, key, StringComparison.Ordinal);
        Assert.DoesNotContain(secrets.Key, key, StringComparison.OrdinalIgnoreCase);
        var entries = codes.Split(';');
        Assert.Equal(10, entries.Length);
        Assert.All(entries, static entry => Assert.StartsWith(HashPrefix, entry));
        Assert.All(
            secrets.RecoveryCodes,
            code => Assert.DoesNotContain(code, codes, StringComparison.OrdinalIgnoreCase));
        using var browser = App.Browser();
        using var signIn = await SignInAsync(browser, twoFactorCode: Code(secrets.Key));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task ACodeOfTheAppIsAcceptedOnce()
    {
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        var code = Code(secrets.Key);
        using var first = App.Browser();
        using var second = App.Browser();
        using var third = App.Browser();

        using var accepted = await SignInAsync(first, twoFactorCode: code);
        using var replayed = await SignInAsync(second, twoFactorCode: code);
        using var next = await SignInAsync(third, twoFactorCode: Code(secrets.Key, NextStep));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(
            "invalid-two-factor-code",
            await ApiJson.ProblemCodeAsync(replayed, HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    // The sign-ins above queue for the hashing admission, one after the other. Here each
    // check reads the account and the step of its last code before either saves, as two
    // requests at once would: the account's concurrency stamp turns the second one down.
    [Fact]
    public async Task OfTwoChecksOfOneCodeReadAtOnceOnlyOneSucceeds()
    {
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        await using var earlier = App.Services.CreateAsyncScope();
        Assert.True(await VerifyAsync(await ReadAsync(earlier), Code(secrets.Key)));
        await using var first = App.Services.CreateAsyncScope();
        await using var second = App.Services.CreateAsyncScope();
        var firstCheck = await ReadAsync(first);
        var secondCheck = await ReadAsync(second);
        var code = Code(secrets.Key, NextStep);

        var accepted = (await VerifyAsync(firstCheck, code), await VerifyAsync(secondCheck, code));

        Assert.Equal((true, false), accepted);
    }

    [Fact]
    public async Task ARecoveryCodeLeavesTheOthersHashed()
    {
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        using var browser = App.Browser();

        using var signIn = await SignInAsync(browser, recoveryCode: secrets.RecoveryCodes[3]);

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var entries = (await StoredAsync(IdentityTokenNames.RecoveryCodes)).Split(';');
        Assert.Equal(9, entries.Length);
        Assert.All(entries, static entry => Assert.StartsWith(HashPrefix, entry));
    }

    [Fact]
    public async Task SecretsLeftInClearAreProtectedOnceAndStillSignIn()
    {
        var account = await App.SeedAsync(_account);
        await StoreInClearAsync(account.Id);
        using var before = App.Browser();
        using var beforeProtection = await SignInAsync(before, twoFactorCode: Code(ClearKey));

        var protectedFirst = await ProtectAsync();
        var sealedKey = await StoredAsync(IdentityTokenNames.AuthenticatorKey);
        var hashedCodes = await StoredAsync(IdentityTokenNames.RecoveryCodes);
        var protectedAgain = await ProtectAsync();

        Assert.Equal(HttpStatusCode.OK, beforeProtection.StatusCode);
        Assert.Equal((1, 0), (protectedFirst, protectedAgain));
        Assert.StartsWith(
            AuthenticatorKeyProtection.SealedPrefix,
            sealedKey,
            StringComparison.Ordinal);
        Assert.DoesNotContain(ClearKey, sealedKey, StringComparison.Ordinal);
        Assert.All(hashedCodes.Split(';'), static entry => Assert.StartsWith(HashPrefix, entry));
        Assert.Equal(sealedKey, await StoredAsync(IdentityTokenNames.AuthenticatorKey));
        Assert.Equal(hashedCodes, await StoredAsync(IdentityTokenNames.RecoveryCodes));
        await SignsInAfterwardsAsync();
    }

    private Task<HttpResponseMessage> SignInAsync(
        BrowserClient browser,
        string? twoFactorCode = null,
        string? recoveryCode = null) =>
        browser.PostAsync("/api/account/login", new
        {
            identifier = _account.Username,
            password = AccountSeed.StrongPassword,
            twoFactorCode,
            recoveryCode,
        });

    // The account as a request reads it, the step of its last code included.
    private async Task<AccountCheck> ReadAsync(AsyncServiceScope scope)
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var account = await users.FindByNameAsync(_account.Username);
        await users.GetAuthenticationTokenAsync(
            account!,
            IdentityTokenNames.LoDbProvider,
            IdentityTokenNames.AuthenticatorStep);
        return new AccountCheck(users, account!);
    }

    private static Task<bool> VerifyAsync(AccountCheck check, string code) =>
        check.Users.VerifyTwoFactorTokenAsync(
            check.Account,
            check.Users.Options.Tokens.AuthenticatorTokenProvider,
            code);

    // As the host's clock reads it, which the provider of the codes follows.
    private string Code(string key, TimeSpan? later = null) =>
        Totp.Code(key, App.Clock.GetUtcNow() + (later ?? TimeSpan.Zero));

    // The code of the next step: the one of this step was spent before the protection.
    private async Task SignsInAfterwardsAsync()
    {
        using var withCode = App.Browser();
        using var codeAfter = await SignInAsync(withCode, twoFactorCode: Code(ClearKey, NextStep));
        using var withRecovery = App.Browser();
        using var recoveryAfter = await SignInAsync(withRecovery, recoveryCode: "pqrtv-wxy23");

        Assert.Equal(HttpStatusCode.OK, codeAfter.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recoveryAfter.StatusCode);
    }

    // The rows an earlier version wrote: Identity's names, the values in clear.
    private async Task StoreInClearAsync(int accountId)
    {
        await using var database = App.Database();
        await database.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO identity_user_tokens (user_id, login_provider, name, value)
            VALUES ({accountId}, '[AspNetUserStore]', 'AuthenticatorKey', {ClearKey}),
                   ({accountId}, '[AspNetUserStore]', 'RecoveryCodes', {ClearCodes});
            """,
            Cancellation);
        await database.Database.ExecuteSqlAsync(
            $"UPDATE users SET two_factor_enabled = true WHERE id = {accountId}",
            Cancellation);
    }

    // The row of this test's account: the other accounts of the host hold their own.
    private async Task<string> StoredAsync(string name)
    {
        var rows = await App.QueryAsync(
            "SELECT t.value FROM identity_user_tokens t JOIN users u ON u.id = t.user_id"
            + $" WHERE u.username = '{_account.Username}'"
            + $" AND t.login_provider = '[AspNetUserStore]' AND t.name = '{name}'");
        return Assert.Single(rows);
    }

    private async Task<int> ProtectAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AuthenticatorSecretsUpkeep>()
            .ProtectAsync(Cancellation);
    }

    // One request's user manager, and the account as it loaded it.
    private sealed record AccountCheck(UserManager<User> Users, User Account);
}
