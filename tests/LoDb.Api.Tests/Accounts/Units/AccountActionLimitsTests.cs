using System.Net;
using System.Security.Claims;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Security.Limits;
using LoDb.Api.Modules.Billing.Purchases;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.PublicApi.Keys;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Hosting.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The hourly limits of the costly actions of a signed-in account: counted by account,
/// whatever the address, refused with a <c>rate-limited</c> problem once the hour is used up,
/// and tuned under <c>LoDb:RateLimiting</c>.
/// </summary>
public sealed class AccountActionLimitsTests
{
    private const string ActionPath = "/action";
    private const string Account = "17";
    private const string OtherAccount = "18";
    private const string Section = "LoDb:RateLimiting:";

    // Stands in for the account the session names.
    private const string AccountHeader = "X-Test-Account";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static TheoryData<string, int> Limits => new()
    {
        { nameof(BuildLimits.Edit), 60 },
        { nameof(BuildLimits.Vote), 120 },
    };

    [Theory]
    [MemberData(nameof(Limits))]
    public async Task AccountIsRefusedOnceItsHourIsUsedUp(string name, int perHour)
    {
        await using var app = await StartAsync(LimitNamed(name));
        using var client = app.GetTestClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < perHour; attempt++)
        {
            using var allowed = await PostAsync(client, Account);
            statuses.Add(allowed.StatusCode);
        }

        using var refused = await PostAsync(client, Account);
        using var otherAccount = await PostAsync(client, OtherAccount);

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.TooManyRequests));
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.OK, otherAccount.StatusCode);
    }

    [Fact]
    public async Task SettingTunesTheBound()
    {
        var settings = new Dictionary<string, string?> { [Section + "VotesPerHour"] = "2" };
        await using var app = await StartAsync(BuildLimits.Vote, settings);
        using var client = app.GetTestClient();

        using var first = await PostAsync(client, Account);
        using var second = await PostAsync(client, Account);
        using var third = await PostAsync(client, Account);

        Assert.Equal(
            (HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests),
            (first.StatusCode, second.StatusCode, third.StatusCode));
    }

    [Fact]
    public async Task AnonymousCallIsLeftToAuthorization()
    {
        var settings = new Dictionary<string, string?> { [Section + "ApiKeyIssuesPerHour"] = "1" };
        await using var app = await StartAsync(ApiKeyLimits.Issue, settings);
        using var client = app.GetTestClient();

        using var first = await PostAsync(client, account: null);
        using var second = await PostAsync(client, account: null);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
    }

    [Theory]
    [InlineData(nameof(PurchaseLimits.Checkout), "CheckoutsPerHour", "0")]
    [InlineData(nameof(ApiKeyLimits.Issue), "ApiKeyIssuesPerHour", "-1")]
    [InlineData(nameof(BuildLimits.Edit), "BuildEditsPerHour", "1000001")]
    [InlineData(nameof(BuildLimits.Vote), "VotesPerHour", "0")]
    public void BoundOutOfRangeStopsTheHost(string name, string setting, string value)
    {
        var limit = LimitNamed(name);

        StartupValidation.RefusalOf<AccountActionLimitOptions>(
            builder => builder.Services.AddAccountActionLimit(limit),
            new Dictionary<string, string?> { [Section + setting] = value });
    }

    private static AccountActionLimit LimitNamed(string name) => name switch
    {
        nameof(PurchaseLimits.Checkout) => PurchaseLimits.Checkout,
        nameof(ApiKeyLimits.Issue) => ApiKeyLimits.Issue,
        nameof(BuildLimits.Edit) => BuildLimits.Edit,
        nameof(BuildLimits.Vote) => BuildLimits.Vote,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    // The policy as a module registers it, behind the host's middleware order.
    private static async Task<WebApplication> StartAsync(
        AccountActionLimit limit,
        Dictionary<string, string?>? settings = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings ?? []);
        builder.Services.AddProblemDetails();
        builder.Services.AddLoDbRateLimiting(builder.Configuration);
        builder.Services.AddAccountActionLimit(limit);
        var app = builder.Build();
        app.Use(static (context, next) =>
        {
            var account = context.Request.Headers[AccountHeader].ToString();
            if (account.Length > 0)
            {
                Claim[] claims = [new(ClaimTypes.NameIdentifier, account)];
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            }

            return next(context);
        });
        app.UseRateLimiter();
        app.MapPost(ActionPath, static () => TypedResults.Ok()).RequireRateLimiting(limit.Policy);
        await app.StartAsync(Cancellation);
        return app;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string? account)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(ActionPath, UriKind.Relative));
        if (account is not null)
        {
            request.Headers.Add(AccountHeader, account);
        }

        return await client.SendAsync(request, Cancellation);
    }
}
