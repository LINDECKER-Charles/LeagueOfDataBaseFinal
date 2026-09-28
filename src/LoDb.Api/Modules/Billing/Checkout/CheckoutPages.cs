using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>
/// The pages of the site a Checkout session returns to, in the buyer's locale: the thanks and
/// cancel pages of a donation, the API portal with its <c>status</c> for a purchase.
/// </summary>
internal static class CheckoutPages
{
    public const string PackSuccess = "pack_success";
    public const string PlanSuccess = "plan_success";
    public const string Cancelled = "cancelled";

    private const string PortalPath = "account/api";

    /// <param name="origin">The site's origin, without a trailing slash.</param>
    /// <param name="locale">The locale the buyer comes back to.</param>
    public static CheckoutPage Donation(string origin, UiLocale locale)
    {
        var root = Root(origin, locale);
        return new CheckoutPage(
            ProductNames.Donation(locale),
            root + "donate/success",
            root + "donate/cancel");
    }

    public static CheckoutPage Pack(string origin, UiLocale locale, ApiPackTerms pack) =>
        new(
            ProductNames.Pack(locale, pack),
            Portal(origin, locale, PackSuccess),
            Portal(origin, locale, Cancelled));

    public static CheckoutPage Plan(string origin, UiLocale locale, ApiPlanTerms plan) =>
        new(
            ProductNames.Plan(locale, plan),
            Portal(origin, locale, PlanSuccess),
            Portal(origin, locale, Cancelled));

    private static string Portal(string origin, UiLocale locale, string status) =>
        Root(origin, locale) + PortalPath + "?status=" + status;

    private static string Root(string origin, UiLocale locale) =>
        origin + "/" + UiLocales.Code(locale) + "/";
}
