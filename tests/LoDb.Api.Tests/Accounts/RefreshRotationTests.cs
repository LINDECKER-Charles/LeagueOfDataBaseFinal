using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Api.Modules.Accounts.Tokens;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Workers;
using LoDb.Api.Workers.Accounts;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The refresh tokens of the apps serve once: each refresh hands out the next one, a spent
/// one played again revokes its whole family, a sign-out revokes the family of the app, and
/// a lost answer or two refreshes at once are forgiven for a few seconds.
/// </summary>
public sealed class RefreshRotationTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string Invalid = "invalid-refresh-token";
    private const string RevokedCounter = "lodb.sessions.revoked";
    private static readonly TimeSpan PastGrace = TimeSpan.FromSeconds(31);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachRefreshHandsOutTheNextToken()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var first = await AppTokens.SignInAsync(app);

        var second = await AppTokens.RenewAsync(app, first);
        var third = await AppTokens.RenewAsync(app, second);

        Assert.NotEqual(Refresh(first), Refresh(second));
        Assert.NotEqual(Refresh(second), Refresh(third));
        var family = Assert.Single(await FamiliesAsync());
        Assert.Equal((2, (DateTimeOffset?)null), (family.Generation, family.RevokedAt));
    }

    [Fact]
    public async Task ASpentTokenPlayedAgainRevokesTheFamily()
    {
        await App.SeedAsync(new AccountSeed());
        using var revoked = App.Security<long>(RevokedCounter);
        using var app = App.App();
        var stolen = await AppTokens.SignInAsync(app);
        var current = await AppTokens.RenewAsync(app, stolen);

        App.Clock.Advance(PastGrace);
        using var replay = await AppTokens.RefreshAsync(app, stolen);
        using var afterReplay = await AppTokens.RefreshAsync(app, current);

        Assert.Equal(Invalid, await ApiJson.ProblemCodeAsync(replay, HttpStatusCode.Unauthorized));
        Assert.Equal(
            Invalid,
            await ApiJson.ProblemCodeAsync(afterReplay, HttpStatusCode.Unauthorized));
        Assert.NotNull(Assert.Single(await FamiliesAsync()).RevokedAt);
        var counted = Assert.Single(revoked.GetMeasurementSnapshot());
        Assert.Equal("refresh_replayed", counted.Tags["reason"]);
    }

    [Fact]
    public async Task ALostAnswerOrTwoRefreshesAtOnceAreForgivenWithinTheGrace()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var tokens = await AppTokens.SignInAsync(app);

        var answers = await Task.WhenAll(
            AppTokens.RefreshAsync(app, tokens),
            AppTokens.RefreshAsync(app, tokens));
        var renewed = await Task.WhenAll(
            answers.Select(static answer => ApiJson.ReadAsync(answer, HttpStatusCode.OK)));
        var next = await AppTokens.RenewAsync(app, renewed[1]);

        Assert.Equal(AccountSeed.Name, await SignedInAsAsync(app, next));
        Assert.Null(Assert.Single(await FamiliesAsync()).RevokedAt);
        foreach (var answer in answers)
        {
            answer.Dispose();
        }
    }

    [Fact]
    public async Task SignOutRevokesTheFamilyOfThatAppOnly()
    {
        await App.SeedAsync(new AccountSeed());
        using var phone = App.App();
        using var desktop = App.App();
        var phoneTokens = await AppTokens.SignInAsync(phone);
        var desktopTokens = await AppTokens.SignInAsync(desktop);

        using var signOut = await AppTokens.SendAsync(
            phone,
            HttpMethod.Post,
            "logout",
            phoneTokens);
        using var phoneRefresh = await AppTokens.RefreshAsync(phone, phoneTokens);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(
            Invalid,
            await ApiJson.ProblemCodeAsync(phoneRefresh, HttpStatusCode.Unauthorized));
        await AppTokens.RenewAsync(desktop, desktopTokens);
    }

    [Fact]
    public async Task ATokenOfBeforeTheFamiliesServesOnceToo()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var legacy = await LegacyRefreshTokenAsync(account);

        using var first = await RefreshAsync(app, legacy);
        App.Clock.Advance(PastGrace);
        using var again = await RefreshAsync(app, legacy);

        var renewed = await ApiJson.ReadAsync(first, HttpStatusCode.OK);
        Assert.Equal(Invalid, await ApiJson.ProblemCodeAsync(again, HttpStatusCode.Unauthorized));
        Assert.Equal(AccountSeed.Name, await SignedInAsAsync(app, renewed));
    }

    [Fact]
    public async Task ATokenOfBeforeTheFamiliesServesOnceWhateverItsSpelling()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var legacy = await LegacyRefreshTokenAsync(account);

        using var first = await RefreshAsync(app, legacy);
        await ApiJson.ReadAsync(first, HttpStatusCode.OK);
        App.Clock.Advance(PastGrace);

        foreach (var spelling in OtherSpellingsOf(legacy))
        {
            using var again = await RefreshAsync(app, spelling);
            Assert.Equal(
                Invalid,
                await ApiJson.ProblemCodeAsync(again, HttpStatusCode.Unauthorized));
        }
    }

    [Fact]
    public async Task FamiliesWhoseTokensRanOutArePurged()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        await AppTokens.SignInAsync(app);
        App.Clock.Advance(TimeSpan.FromDays(29));
        await AppTokens.SignInAsync(app);

        App.Clock.Advance(TimeSpan.FromDays(3));
        var purged = await PurgeAsync();

        Assert.Equal(1, purged);
        Assert.Single(await FamiliesAsync());
    }

    [Fact]
    public void PurgeIsDiscoveredAsAWorker() =>
        Assert.Contains(
            typeof(RefreshFamilyPurgeJob),
            ConventionalWorkers.Discover(typeof(Program).Assembly));

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(postgres, withGoogle: false);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static string? Refresh(JsonElement tokens) => ApiJson.Text(tokens, "refreshToken");

    private static async Task<string?> SignedInAsAsync(HttpClient app, JsonElement tokens)
    {
        using var response = await AppTokens.SendAsync(app, HttpMethod.Get, "me", tokens);
        var user = (await ApiJson.ReadAsync(response, HttpStatusCode.OK)).GetProperty("user");
        return user.ValueKind == JsonValueKind.Null ? null : ApiJson.Text(user, "username");
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient app, string refreshToken) =>
        app.PostAsJsonAsync(
            new Uri("/api/account/refresh", UriKind.Relative),
            new { refreshToken },
            Cancellation);

    // Other texts a base64url decoder reads as the same bytes: the standard alphabet, and the
    // padding the URL-safe form leaves out.
    private static IEnumerable<string> OtherSpellingsOf(string token)
    {
        const int Quantum = 4;
        yield return token.Replace('-', '+').Replace('_', '/');
        yield return token + new string('=', (Quantum - (token.Length % Quantum)) % Quantum);
    }

    // A refresh token as the API handed them out before the families: no family named.
    private async Task<string> LegacyRefreshTokenAsync(User account)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var principal = await services.GetRequiredService<SignInManager<User>>()
            .CreateUserPrincipalAsync(account);
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = App.Clock.GetUtcNow() + TimeSpan.FromDays(30),
        };
        var ticket = new AuthenticationTicket(principal, properties, "Identity.Bearer:Refresh");
        return services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(IdentityConstants.BearerScheme)
            .RefreshTokenProtector
            .Protect(ticket);
    }

    private async Task<List<RefreshTokenFamily>> FamiliesAsync()
    {
        await using var database = App.Database();
        return await database.RefreshTokenFamilies.AsNoTracking().ToListAsync(Cancellation);
    }

    private async Task<int> PurgeAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RefreshFamilies>()
            .PurgeAsync(Cancellation);
    }
}
