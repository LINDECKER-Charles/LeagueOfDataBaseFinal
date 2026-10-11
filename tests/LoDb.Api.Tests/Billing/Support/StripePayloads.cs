using System.Globalization;
using System.Text.Json.Nodes;
using LoDb.Api.Modules.Billing.Catalog;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// Stripe's events as its servers post them, with only the fields the site reads; a test
/// alters a session before wrapping it in its event. Sessions charge the catalogue's price.
/// </summary>
public static class StripePayloads
{
    public const string CheckoutCompleted = "checkout.session.completed";
    public const string AsyncPaymentSucceeded = "checkout.session.async_payment_succeeded";
    public const string AsyncPaymentFailed = "checkout.session.async_payment_failed";
    public const string SubscriptionUpdated = "customer.subscription.updated";
    public const string SubscriptionDeleted = "customer.subscription.deleted";
    public const string InvoicePaymentFailed = "invoice.payment_failed";
    public const string ChargeRefunded = "charge.refunded";
    public const string DisputeCreated = "charge.dispute.created";
    public const string Customer = "cus_test_buyer";
    public const string Subscription = "sub_test_plan";

    /// <summary>When every event of these tests was created, which dates the payments.</summary>
    public static readonly DateTimeOffset CreatedAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>An event wrapping <paramref name="data"/>, the object it is about.</summary>
    public static JsonObject Event(string id, string type, JsonObject data) => new()
    {
        ["id"] = id,
        ["object"] = "event",
        ["api_version"] = "2025-01-27.acacia",
        ["created"] = CreatedAt.ToUnixTimeSeconds(),
        ["livemode"] = false,
        ["pending_webhooks"] = 1,
        ["type"] = type,
        ["data"] = new JsonObject { ["object"] = data },
    };

    /// <summary><paramref name="stripeEvent"/>, created <paramref name="days"/> later.</summary>
    public static JsonObject Later(JsonObject stripeEvent, int days)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        stripeEvent["created"] = CreatedAt.AddDays(days).ToUnixTimeSeconds();
        return stripeEvent;
    }

    /// <summary>The completion of <paramref name="session"/>.</summary>
    public static JsonObject Completed(string id, JsonObject session) =>
        Event(id, CheckoutCompleted, session);

    /// <summary>The end of the subscription <paramref name="subscriptionId"/>.</summary>
    public static JsonObject Cancelled(string id, string subscriptionId = Subscription) =>
        Event(id, SubscriptionDeleted, SubscriptionObject(subscriptionId, "canceled"));

    /// <summary>The subscription of these tests, now <paramref name="status"/>.</summary>
    public static JsonObject Updated(string id, string status) =>
        Event(id, SubscriptionUpdated, SubscriptionObject(Subscription, status));

    /// <summary>A renewal of <paramref name="subscriptionId"/> that could not be charged.</summary>
    public static JsonObject InvoiceFailed(string id, string subscriptionId = Subscription) =>
        Event(id, InvoicePaymentFailed, new JsonObject
        {
            ["id"] = "in_test_" + id,
            ["object"] = "invoice",
            ["customer"] = Customer,
            ["status"] = "open",
            ["parent"] = new JsonObject
            {
                ["type"] = "subscription_details",
                ["subscription_details"] = new JsonObject { ["subscription"] = subscriptionId },
            },
        });

    /// <summary>The full refund of a payment of 10 euros, by its payment and customer.</summary>
    public static JsonObject Refunded(string id, string? paymentIntent, string? customer = null) =>
        Event(id, ChargeRefunded, new JsonObject
        {
            ["id"] = "ch_test_" + id,
            ["object"] = "charge",
            ["amount"] = 1_000,
            ["amount_refunded"] = 1_000,
            ["refunded"] = true,
            ["currency"] = "eur",
            ["payment_intent"] = paymentIntent,
            ["customer"] = customer,
        });

    /// <summary>A dispute of the payment <paramref name="paymentIntent"/>.</summary>
    public static JsonObject Disputed(string id, string paymentIntent) =>
        Event(id, DisputeCreated, new JsonObject
        {
            ["id"] = "dp_test_" + id,
            ["object"] = "dispute",
            ["amount"] = 1_000,
            ["currency"] = "eur",
            ["charge"] = "ch_test_" + id,
            ["payment_intent"] = paymentIntent,
            ["reason"] = "fraudulent",
            ["status"] = "needs_response",
        });

    /// <summary>The payment a session in payment mode is paid by, in these tests.</summary>
    public static string PaymentOf(string sessionId) => "pi_for_" + sessionId;

    /// <summary>A paid session in payment mode, with <paramref name="metadata"/>.</summary>
    public static JsonObject Session(string id, JsonObject metadata) => new()
    {
        ["id"] = id,
        ["object"] = "checkout.session",
        ["mode"] = "payment",
        ["payment_status"] = "paid",
        ["status"] = "complete",
        ["amount_total"] = 500,
        ["currency"] = "eur",
        ["payment_intent"] = PaymentOf(id),
        ["metadata"] = metadata,
    };

    /// <summary>A credit pack bought by <paramref name="buyerId"/>, at its price.</summary>
    public static JsonObject Pack(string id, int buyerId, long requests)
    {
        var session = Session(id, new JsonObject
        {
            ["user_id"] = Text(buyerId),
            ["kind"] = "api_pack",
            ["requests"] = requests.ToString(CultureInfo.InvariantCulture),
        });
        session["client_reference_id"] = Text(buyerId);
        session["amount_total"] = ApiPacks.All
            .FirstOrDefault(pack => pack.Requests == requests)?.PriceCents ?? 0;
        return session;
    }

    /// <summary>
    /// A subscription to <paramref name="plan"/> by <paramref name="buyerId"/>, at its price.
    /// </summary>
    public static JsonObject Plan(string id, int buyerId, string plan)
    {
        var session = Session(id, new JsonObject
        {
            ["user_id"] = Text(buyerId),
            ["kind"] = "api_plan",
            ["plan"] = plan,
        });
        session["mode"] = "subscription";
        session["client_reference_id"] = Text(buyerId);
        session["customer"] = Customer;
        session["subscription"] = Subscription;
        session["payment_intent"] = null;
        session["amount_total"] = ApiPlans.Find(plan)?.PriceCents ?? 0;
        return session;
    }

    /// <summary>A donation of <paramref name="amountCents"/>, signed in or not.</summary>
    public static JsonObject Donation(string id, int amountCents, int? donorId = null)
    {
        var session = Session(id, new JsonObject
        {
            ["source"] = "lodb-donate",
            ["kind"] = "donation",
        });
        session["amount_total"] = amountCents;
        session["client_reference_id"] = donorId is { } donor ? Text(donor) : null;
        return session;
    }

    private static JsonObject SubscriptionObject(string id, string status) => new()
    {
        ["id"] = id,
        ["object"] = "subscription",
        ["customer"] = Customer,
        ["status"] = status,
    };

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
}
