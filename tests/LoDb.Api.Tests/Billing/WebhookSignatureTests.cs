using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// <c>POST /webhooks/stripe</c> before any handler: only a payload signed with the secret,
/// less than five minutes ago, is read; a type the site does not handle is recorded as
/// ignored and acknowledged.
/// </summary>
public sealed class WebhookSignatureTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string OtherSecret = "whsec_another_placeholder";

    private BillingApp? _app;

    private BillingApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("missing")]
    [InlineData("garbage")]
    [InlineData("other-secret")]
    [InlineData("tampered")]
    [InlineData("stale")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task PayloadsStripeDidNotSignNowAreRejected(string forgery)
    {
        var payload = UnhandledEvent("evt_forged").ToJsonString();
        var now = App.Clock.GetUtcNow();
        var (body, signature) = forgery switch
        {
            "missing" => (payload, null),
            "garbage" => (payload, "t=abc,v1=xyz"),
            "other-secret" => (payload, WebhookSigner.Sign(payload, now, OtherSecret)),
            "tampered" => (
                payload.Replace("evt_forged", "evt_other", StringComparison.Ordinal),
                Sign(payload, now)),
            "stale" => (payload, Sign(payload, now.AddSeconds(-301))),
            // Signed, but no event.
            _ => (forgery, Sign(forgery, now)),
        };

        using var response = await App.PostWebhookAsync(body, signature);

        Assert.Equal(
            "invalid-signature",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.BadRequest));
        Assert.Empty(await EventsAsync());
    }

    [Fact]
    public async Task SignatureOfFourMinutesAgoIsAccepted()
    {
        var payload = UnhandledEvent("evt_recent").ToJsonString();
        var signature = Sign(payload, App.Clock.GetUtcNow().AddMinutes(-4));

        using var response = await App.PostWebhookAsync(payload, signature);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledTypeIsRecordedAsIgnoredAndAcknowledged()
    {
        using var response = await App.DeliverAsync(UnhandledEvent("evt_invoice"));

        var receipt = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.True(receipt.GetProperty("received").GetBoolean());
        var recorded = Assert.Single(await EventsAsync());
        Assert.Equal(
            ("evt_invoice", "invoice.paid", StripeEventStatus.Ignored),
            (recorded.Id, recorded.Type, recorded.Status));
        Assert.Equal(StripePayloads.CreatedAt, recorded.CreatedAt);
        // PostgreSQL keeps microseconds.
        Assert.Equal(App.Clock.GetUtcNow(), recorded.ProcessedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task WebhookIsOutOfTheFormsForgeryGuard()
    {
        // No Origin, no cookie, no token: Stripe's servers post as they please.
        using var response = await App.DeliverAsync(UnhandledEvent("evt_guard"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WebhookWithoutSecretAsksStripeToComeBack()
    {
        await using var unconfigured = await BillingApp.StartAsync(postgres, configured: false);
        var payload = UnhandledEvent("evt_early").ToJsonString();

        using var response = await unconfigured.PostWebhookAsync(
            payload,
            Sign(payload, unconfigured.Clock.GetUtcNow()));

        Assert.Equal(
            "webhook-unconfigured",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.ServiceUnavailable));
        await using var db = unconfigured.Database();
        Assert.Empty(await db.StripeEvents.ToListAsync(Cancellation));
    }

    public async ValueTask InitializeAsync() => _app = await BillingApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static JsonObject UnhandledEvent(string id) =>
        StripePayloads.Event(id, "invoice.paid", new JsonObject
        {
            ["id"] = "in_test_1",
            ["object"] = "invoice",
        });

    private static string Sign(string payload, DateTimeOffset at) =>
        WebhookSigner.Sign(payload, at, BillingApp.WebhookSecret);

    private async Task<List<StripeEvent>> EventsAsync()
    {
        await using var db = App.Database();
        return await db.StripeEvents.AsNoTracking().ToListAsync(Cancellation);
    }
}
