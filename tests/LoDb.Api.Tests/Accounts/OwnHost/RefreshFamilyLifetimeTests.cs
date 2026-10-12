using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Api.Modules.Accounts.Tokens;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.OwnHost;

/// <summary>
/// The families of the refresh tokens past the grace of a few seconds: a spent token played
/// again revokes its whole family, and is counted; a token of before the families serves
/// once, whatever its spelling; the families whose tokens ran out are purged.
/// </summary>
/// <remarks>A host per test: each advances its clock, one reads a meter.</remarks>
public sealed class RefreshFamilyLifetimeTests(PostgresContainerFixture postgres)
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
    public async Task ATokenOfBeforeTheFamiliesServesOnceWhateverItsSpelling()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var legacy = await LegacyRefreshTokenAsync(account);

        using var first = await RefreshAsync(app, legacy);
        var renewed = await ApiJson.ReadAsync(first, HttpStatusCode.OK);
        App.Clock.Advance(PastGrace);

        foreach (var spelling in (string[])[legacy, .. OtherSpellingsOf(legacy)])
        {
            using var again = await RefreshAsync(app, spelling);
            Assert.Equal(
                Invalid,
                await ApiJson.ProblemCodeAsync(again, HttpStatusCode.Unauthorized));
        }

        Assert.Equal(AccountSeed.Name, await SignedInAsAsync(app, renewed));
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

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(postgres, withGoogle: false);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

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
