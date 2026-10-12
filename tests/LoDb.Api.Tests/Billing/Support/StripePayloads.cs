using System.Globalization;
using System.Text.Json.Nodes;
using LoDb.Api.Modules.Billing.Catalog;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// Stripe's events as its servers post them, with only the fields the site reads; a test
/// alters a session before wrapping it in its event. Sessions charge the catalogue's price.
/// </summary>
/// <remarks>
/// Every identifier these payloads give, of an event, a session, a charge, the customer or
/// the subscription, ends with the mark of the test: the tests that share a database never
/// deliver the same event twice nor touch each other's subscription. A test names an
/// identifier as it wrote it (<c>cs_test_pack</c>); <see cref="Id"/> gives it as delivered.
/// </remarks>
/// <param name="mark">What every identifier of the test ends with; none for a test alone.</param>
public sealed class StripePayloads(string mark)
{
    public const string CheckoutCompleted = "checkout.session.completed";
    public const string AsyncPaymentSucceeded = "checkout.session.async_payment_succeeded";
    public const string AsyncPaymentFailed = "checkout.session.async_payment_failed";
    public const string SubscriptionUpdated = "customer.subscription.updated";
    public const string SubscriptionDeleted = "customer.subscription.deleted";
    public const string InvoicePaymentFailed = "invoice.payment_failed";
    public const string ChargeRefunded = "charge.refunded";
    public const string DisputeCreated = "charge.dispute.created";

    /// <summary>When every event of these tests was created, which dates the payments.</summary>
    public static readonly DateTimeOffset CreatedAt = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The customer of the test's subscriptions.</summary>
    public string Customer => Id("cus_test_buyer");

    /// <summary>The subscription of the test.</summary>
    public string Subscription => Id("sub_test_plan");

    /// <summary>The identifier <paramref name="id"/> as the test's payloads give it.</summary>
    public string Id(string id) => id + mark;

    /// <summary><paramref name="stripeEvent"/>, created <paramref name="days"/> later.</summary>
    public static JsonObject Later(JsonObject stripeEvent, int days)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        stripeEvent["created"] = CreatedAt.AddDays(days).ToUnixTimeSeconds();
        return stripeEvent;
    }

    /// <summary>An event wrapping <paramref name="data"/>, the object it is about.</summary>
    public JsonObject Event(string id, string type, JsonObject data) => new()
    {
        ["id"] = Id(id),
        ["object"] = "event",
        ["api_version"] = "2025-01-27.acacia",
        ["created"] = CreatedAt.ToUnixTimeSeconds(),
        ["livemode"] = false,
        ["pending_webhooks"] = 1,
        ["type"] = type,
        ["data"] = new JsonObject { ["object"] = data },
    };

    /// <summary>The completion of <paramref name="session"/>.</summary>
    public JsonObject Completed(string id, JsonObject session) =>
        Event(id, CheckoutCompleted, session);

    /// <summary>
    /// The end of the subscription <paramref name="subscriptionId"/>, the test's by default.
    /// </summary>
    public JsonObject Cancelled(string id, string? subscriptionId = null) =>
        Event(
            id,
            SubscriptionDeleted,
            SubscriptionObject(subscriptionId ?? Subscription, "canceled"));

    /// <summary>The subscription of the test, now <paramref name="status"/>.</summary>
    public JsonObject Updated(string id, string status) =>
        Event(id, SubscriptionUpdated, SubscriptionObject(Subscription, status));

    /// <summary>
    /// A renewal of <paramref name="subscriptionId"/>, the test's by default, that could not
    /// be charged.
    /// </summary>
    public JsonObject InvoiceFailed(string id, string? subscriptionId = null) =>
        Event(id, InvoicePaymentFailed, new JsonObject
        {
            ["id"] = "in_test_" + Id(id),
            ["object"] = "invoice",
            ["customer"] = Customer,
            ["status"] = "open",
            ["parent"] = new JsonObject
            {
                ["type"] = "subscription_details",
                ["subscription_details"] = new JsonObject
                {
                    ["subscription"] = subscriptionId ?? Subscription,
                },
            },
        });

    /// <summary>
    /// The full refund of a payment of 10 euros, by its payment and customer, both named as
    /// delivered (<see cref="PaymentOf"/>, <see cref="Customer"/>).
    /// </summary>
    public JsonObject Refunded(string id, string? paymentIntent, string? customer = null) =>
        Event(id, ChargeRefunded, new JsonObject
        {
            ["id"] = "ch_test_" + Id(id),
            ["object"] = "charge",
            ["amount"] = 1_000,
            ["amount_refunded"] = 1_000,
            ["refunded"] = true,
            ["currency"] = "eur",
            ["payment_intent"] = paymentIntent,
            ["customer"] = customer,
        });

    /// <summary>A dispute of the payment <paramref name="paymentIntent"/>, as delivered.</summary>
    public JsonObject Disputed(string id, string paymentIntent) =>
        Event(id, DisputeCreated, new JsonObject
        {
            ["id"] = "dp_test_" + Id(id),
            ["object"] = "dispute",
            ["amount"] = 1_000,
            ["currency"] = "eur",
            ["charge"] = "ch_test_" + Id(id),
            ["payment_intent"] = paymentIntent,
            ["reason"] = "fraudulent",
            ["status"] = "needs_response",
        });

    /// <summary>The payment of the session <paramref name="sessionId"/>, as delivered.</summary>
    public string PaymentOf(string sessionId) => "pi_for_" + Id(sessionId);

    /// <summary>A paid session in payment mode, with <paramref name="metadata"/>.</summary>
    public JsonObject Session(string id, JsonObject metadata) => new()
    {
        ["id"] = Id(id),
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
    public JsonObject Pack(string id, int buyerId, long requests)
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
    public JsonObject Plan(string id, int buyerId, string plan)
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
    public JsonObject Donation(string id, int amountCents, int? donorId = null)
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

    private JsonObject SubscriptionObject(string id, string status) => new()
    {
        ["id"] = id,
        ["object"] = "subscription",
        ["customer"] = Customer,
        ["status"] = status,
    };

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
}
