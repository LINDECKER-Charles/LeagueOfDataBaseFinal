using System.Globalization;
using System.Text.Json.Nodes;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// Stripe's events as its servers post them, with only the fields the site reads; a test
/// alters a session before wrapping it in its event.
/// </summary>
public static class StripePayloads
{
    public const string CheckoutCompleted = "checkout.session.completed";
    public const string SubscriptionDeleted = "customer.subscription.deleted";
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

    /// <summary>The completion of <paramref name="session"/>.</summary>
    public static JsonObject Completed(string id, JsonObject session) =>
        Event(id, CheckoutCompleted, session);

    /// <summary>The end of the subscription <paramref name="subscriptionId"/>.</summary>
    public static JsonObject Cancelled(string id, string subscriptionId = Subscription) =>
        Event(id, SubscriptionDeleted, new JsonObject
        {
            ["id"] = subscriptionId,
            ["object"] = "subscription",
            ["customer"] = Customer,
            ["status"] = "canceled",
        });

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
        ["metadata"] = metadata,
    };

    /// <summary>A credit pack bought by <paramref name="buyerId"/>.</summary>
    public static JsonObject Pack(string id, int buyerId, long requests)
    {
        var session = Session(id, new JsonObject
        {
            ["user_id"] = Text(buyerId),
            ["kind"] = "api_pack",
            ["requests"] = requests.ToString(CultureInfo.InvariantCulture),
        });
        session["client_reference_id"] = Text(buyerId);
        return session;
    }

    /// <summary>A subscription to <paramref name="plan"/> by <paramref name="buyerId"/>.</summary>
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

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
}
