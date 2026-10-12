using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The refresh tokens of the apps serve once: each refresh hands out the next one, a sign-out
/// revokes the family of the app, and a lost answer or two refreshes at once are forgiven.
/// What happens past the grace is <see cref="OwnHost.RefreshFamilyLifetimeTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with an account of its own.</remarks>
public sealed class RefreshRotationTests(SharedAccountsApp host)
    : IClassFixture<SharedAccountsApp>
{
    private const string Invalid = "invalid-refresh-token";

    private readonly AccountSeed _account = host.Unique();

    private AccountsApp App { get; } = host.App;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachRefreshHandsOutTheNextToken()
    {
        var account = await App.SeedAsync(_account);
        using var app = App.App();
        var first = await AppTokens.SignInAsync(app, _account.Username);

        var second = await AppTokens.RenewAsync(app, first);
        var third = await AppTokens.RenewAsync(app, second);

        Assert.NotEqual(Refresh(first), Refresh(second));
        Assert.NotEqual(Refresh(second), Refresh(third));
        var family = Assert.Single(await FamiliesOfAsync(account));
        Assert.Equal((2, (DateTimeOffset?)null), (family.Generation, family.RevokedAt));
    }

    [Fact]
    public async Task ALostAnswerOrTwoRefreshesAtOnceAreForgivenWithinTheGrace()
    {
        var account = await App.SeedAsync(_account);
        using var app = App.App();
        var tokens = await AppTokens.SignInAsync(app, _account.Username);

        var answers = await Task.WhenAll(
            AppTokens.RefreshAsync(app, tokens),
            AppTokens.RefreshAsync(app, tokens));
        var renewed = await Task.WhenAll(
            answers.Select(static answer => ApiJson.ReadAsync(answer, HttpStatusCode.OK)));
        var next = await AppTokens.RenewAsync(app, renewed[1]);

        Assert.Equal(_account.Username, await SignedInAsAsync(app, next));
        Assert.Null(Assert.Single(await FamiliesOfAsync(account)).RevokedAt);
        foreach (var answer in answers)
        {
            answer.Dispose();
        }
    }

    [Fact]
    public async Task SignOutRevokesTheFamilyOfThatAppOnly()
    {
        await App.SeedAsync(_account);
        using var phone = App.App();
        using var desktop = App.App();
        var phoneTokens = await AppTokens.SignInAsync(phone, _account.Username);
        var desktopTokens = await AppTokens.SignInAsync(desktop, _account.Username);

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

    private static string? Refresh(JsonElement tokens) => ApiJson.Text(tokens, "refreshToken");

    private static async Task<string?> SignedInAsAsync(HttpClient app, JsonElement tokens)
    {
        using var response = await AppTokens.SendAsync(app, HttpMethod.Get, "me", tokens);
        var user = (await ApiJson.ReadAsync(response, HttpStatusCode.OK)).GetProperty("user");
        return user.ValueKind == JsonValueKind.Null ? null : ApiJson.Text(user, "username");
    }

    private async Task<List<RefreshTokenFamily>> FamiliesOfAsync(User account)
    {
        await using var database = App.Database();
        return await database.RefreshTokenFamilies.AsNoTracking()
            .Where(family => family.UserId == account.Id)
            .ToListAsync(Cancellation);
    }
}
