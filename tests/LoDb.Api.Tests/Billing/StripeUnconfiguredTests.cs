using System.Net;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// A site without its Stripe settings: the donation form says it is closed and opens no
/// checkout, and the webhook asks Stripe to come back rather than reading anything.
/// </summary>
public sealed class StripeUnconfiguredTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task WithoutStripeTheFormIsClosed()
    {
        await using var closed = await BillingApp.StartAsync(postgres, configured: false);
        using var browser = closed.Browser();

        using var options = await browser.GetAsync("/api/donations/options");
        using var checkout = await browser.PostAsync(
            "/api/donations/checkout",
            new { amountCents = 500 });

        Assert.False(
            (await ApiJson.ReadAsync(options, HttpStatusCode.OK)).GetProperty("available")
                .GetBoolean());
        Assert.Equal(
            "payments-unavailable",
            await ApiJson.ProblemCodeAsync(checkout, HttpStatusCode.ServiceUnavailable));
        Assert.Empty(closed.Gateway.Sessions);
    }

    [Fact]
    public async Task WebhookWithoutSecretAsksStripeToComeBack()
    {
        await using var unconfigured = await BillingApp.StartAsync(postgres, configured: false);
        var stripe = new StripePayloads(string.Empty);
        var payload = stripe.Event("evt_early", "invoice.paid", new JsonObject
        {
            ["id"] = "in_test_1",
            ["object"] = "invoice",
        }).ToJsonString();

        using var response = await unconfigured.PostWebhookAsync(
            payload,
            WebhookSigner.Sign(payload, unconfigured.Clock.GetUtcNow(), BillingApp.WebhookSecret));

        Assert.Equal(
            "webhook-unconfigured",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.ServiceUnavailable));
        await using var db = unconfigured.Database();
        Assert.Empty(await db.StripeEvents.ToListAsync(TestContext.Current.CancellationToken));
    }
}
