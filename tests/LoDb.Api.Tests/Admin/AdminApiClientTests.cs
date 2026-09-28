using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/api-clients</c>: the keys of the public API, revoked or credited, each change
/// effective on <c>/v1</c> from the next request.
/// </summary>
public sealed class AdminApiClientTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string ClientsPath = "/api/admin/api-clients";
    private const string UsagePath = "/v1/usage";

    [Fact]
    public async Task TheListShowsEachKeyWithItsOwnerAndTheFleetCounters()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var (id, secret) = await AdminSeed.ApiKeyAsync(App, owner.Id);

        var page = await ReadAsync(admin, ClientsPath);

        var key = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(
            (id, secret[..12], "free", true, AccountSeed.Name),
            (key.GetProperty("id").GetInt32(), ApiJson.Text(key, "keyPrefix"),
                ApiJson.Text(key, "plan"), key.GetProperty("isActive").GetBoolean(),
                ApiJson.Text(key.GetProperty("owner"), "username")));
        var kpis = page.GetProperty("kpis");
        Assert.Equal(1, kpis.GetProperty("active").GetInt32());
        var plan = Assert.Single(kpis.GetProperty("byPlan").EnumerateArray());
        Assert.Equal(
            ("free", 1),
            (ApiJson.Text(plan, "plan"), plan.GetProperty("keys").GetInt32()));
    }

    [Fact]
    public async Task ARevokedKeyIsRefusedByTheNextRequest()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var (id, secret) = await AdminSeed.ApiKeyAsync(App, owner.Id);
        using var client = KeyClient(secret);
        using var before = await UsageAsync(client);

        using var revoke = await admin.PostAsync($"{ClientsPath}/{id}/revoke");
        using var after = await UsageAsync(client);
        using var again = await admin.PostAsync($"{ClientsPath}/{id}/revoke");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        var body = await after.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains("\"forbidden\"", body, StringComparison.Ordinal);
        Assert.Equal(
            "api-client-revoked",
            await ApiJson.ProblemCodeAsync(again, HttpStatusCode.Conflict));
        var entry = await LastAuditAsync(AuditAction.AdminApiClientRevoke);
        Assert.Equal(
            (AuditTargetType.ApiClient, secret[..12] + "…"),
            (entry.TargetType, entry.Target));
    }

    [Fact]
    public async Task ACreditAddsRequestsDatedByAGrant()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var (id, secret) = await AdminSeed.ApiKeyAsync(App, owner.Id);
        using var client = KeyClient(secret);
        using var before = await UsageAsync(client);

        using var credit = await admin.PostAsync(
            $"{ClientsPath}/{id}/credit",
            new { requests = 5000 });
        using var after = await UsageAsync(client);

        var receipt = await ApiJson.ReadAsync(credit, HttpStatusCode.OK);
        Assert.Equal(
            (5000L, 60),
            (receipt.GetProperty("creditsBalance").GetInt64(),
                receipt.GetProperty("rateLimitPerMin").GetInt32()));
        Assert.Equal(["10"], before.Headers.GetValues("X-RateLimit-Limit"));
        Assert.Equal(["60"], after.Headers.GetValues("X-RateLimit-Limit"));
        var grant = await AdminSeed.WithContextAsync(App, context =>
            context.ApiCreditGrants.AsNoTracking().SingleAsync(Cancellation));
        Assert.Equal(
            (id, ApiCreditGrantSource.Admin, 5000L, (string?)null),
            (grant.ApiKeyId, grant.Source, grant.Requests, grant.StripeSessionId));
        var entry = await LastAuditAsync(AuditAction.AdminApiClientCredit);
        Assert.Equal("5000", RawMeta(entry, "requests"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_001)]
    public async Task ACreditOutOfBoundsIsRefused(long requests)
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var (id, _) = await AdminSeed.ApiKeyAsync(App, owner.Id);

        using var credit = await admin.PostAsync($"{ClientsPath}/{id}/credit", new { requests });

        Assert.Equal(["out-of-range"], (await ApiJson.FieldErrorsAsync(credit))["requests"]);
        Assert.False(await AdminSeed.WithContextAsync(App, context =>
            context.ApiCreditGrants.AnyAsync(Cancellation)));
    }

    [Fact]
    public async Task AnUnknownKeyIsNotFound()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var revoke = await admin.PostAsync($"{ClientsPath}/999999/revoke");
        using var credit = await admin.PostAsync(
            $"{ClientsPath}/999999/credit",
            new { requests = 10 });

        Assert.Equal(
            "api-client-not-found",
            await ApiJson.ProblemCodeAsync(revoke, HttpStatusCode.NotFound));
        Assert.Equal(
            "api-client-not-found",
            await ApiJson.ProblemCodeAsync(credit, HttpStatusCode.NotFound));
    }

    private static Task<HttpResponseMessage> UsageAsync(HttpClient client) =>
        client.GetAsync(new Uri(UsagePath, UriKind.Relative), Cancellation);

    private HttpClient KeyClient(string secret)
    {
        var client = App.App();
        client.DefaultRequestHeaders.Add("X-Api-Key", secret);
        return client;
    }
}
