using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Billing.Payments;

/// <summary>Stored spelling of the payment states, which the check constraints list too.</summary>
internal static class PaymentColumns
{
    public static readonly ValueConverter<StripeCheckoutKind, string> KindConverter = new(
        kind => ToText(kind),
        value => ParseKind(value));

    public static readonly ValueConverter<StripeCheckoutStatus, string> CheckoutConverter = new(
        status => ToText(status),
        value => ParseCheckout(value));

    public static readonly ValueConverter<StripeSubscriptionStatus, string>
        SubscriptionConverter = new(
            status => ToText(status),
            value => ParseSubscription(value));

    private static string ToText(StripeCheckoutKind kind) => kind switch
    {
        StripeCheckoutKind.Pack => "api_pack",
        StripeCheckoutKind.Plan => "api_plan",
        StripeCheckoutKind.Donation => "donation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static StripeCheckoutKind ParseKind(string value) => value switch
    {
        "api_pack" => StripeCheckoutKind.Pack,
        "api_plan" => StripeCheckoutKind.Plan,
        "donation" => StripeCheckoutKind.Donation,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static string ToText(StripeCheckoutStatus status) => status switch
    {
        StripeCheckoutStatus.Pending => "pending",
        StripeCheckoutStatus.Paid => "paid",
        StripeCheckoutStatus.Failed => "failed",
        StripeCheckoutStatus.Refused => "refused",
        StripeCheckoutStatus.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static StripeCheckoutStatus ParseCheckout(string value) => value switch
    {
        "pending" => StripeCheckoutStatus.Pending,
        "paid" => StripeCheckoutStatus.Paid,
        "failed" => StripeCheckoutStatus.Failed,
        "refused" => StripeCheckoutStatus.Refused,
        "revoked" => StripeCheckoutStatus.Revoked,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static string ToText(StripeSubscriptionStatus status) => status switch
    {
        StripeSubscriptionStatus.Entitled => "entitled",
        StripeSubscriptionStatus.Suspended => "suspended",
        StripeSubscriptionStatus.Ended => "ended",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static StripeSubscriptionStatus ParseSubscription(string value) => value switch
    {
        "entitled" => StripeSubscriptionStatus.Entitled,
        "suspended" => StripeSubscriptionStatus.Suspended,
        "ended" => StripeSubscriptionStatus.Ended,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
