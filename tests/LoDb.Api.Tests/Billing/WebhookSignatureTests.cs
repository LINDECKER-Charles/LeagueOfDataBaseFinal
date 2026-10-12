using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Infrastructure.Persistence.Billing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// <c>POST /webhooks/stripe</c> before any handler: only a payload signed with the secret,
/// less than five minutes ago, is read; a type the site does not handle is recorded as
/// ignored and acknowledged.
/// </summary>
/// <remarks>The tests share a host, each with Stripe identifiers of its own.</remarks>
public sealed class WebhookSignatureTests : IClassFixture<SharedBillingApp>
{
    private const string OtherSecret = "whsec_another_placeholder";

    private readonly StripePayloads _stripe;

    public WebhookSignatureTests(SharedBillingApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        _stripe = host.Payloads();
    }

    private BillingApp App { get; }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("missing")]
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
        Assert.Empty(await EventsAsync("evt_forged", "evt_other"));
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
        var recorded = Assert.Single(await EventsAsync("evt_invoice"));
        Assert.Equal(
            (_stripe.Id("evt_invoice"), "invoice.paid", StripeEventStatus.Ignored),
            (recorded.Id, recorded.Type, recorded.Status));
        Assert.Equal(StripePayloads.CreatedAt, recorded.CreatedAt);
        // PostgreSQL keeps microseconds.
        Assert.Equal(App.Clock.GetUtcNow(), recorded.ProcessedAt, TimeSpan.FromMilliseconds(1));
    }

    private static string Sign(string payload, DateTimeOffset at) =>
        WebhookSigner.Sign(payload, at, BillingApp.WebhookSecret);

    private JsonObject UnhandledEvent(string id) =>
        _stripe.Event(id, "invoice.paid", new JsonObject
        {
            ["id"] = "in_test_1",
            ["object"] = "invoice",
        });

    // The events the test names, as delivered.
    private async Task<List<StripeEvent>> EventsAsync(params string[] ids)
    {
        var delivered = ids.Select(_stripe.Id).ToList();
        await using var db = App.Database();
        return await db.StripeEvents.AsNoTracking()
            .Where(stripeEvent => delivered.Contains(stripeEvent.Id))
            .ToListAsync(Cancellation);
    }
}
