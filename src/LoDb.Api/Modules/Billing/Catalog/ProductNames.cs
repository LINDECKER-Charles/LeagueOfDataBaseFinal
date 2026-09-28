using System.Globalization;
using System.Text.Json.Nodes;
using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Billing.Catalog;

/// <summary>
/// The names Stripe's payment page shows for what is bought, in the buyer's locale: the texts
/// of the legacy <c>donate.product_name</c>, <c>api.product.*</c> and <c>api.plan.*</c>.
/// </summary>
/// <remarks>
/// A text a locale lacks is taken from English, as the front's catalogues do. The pack keeps
/// the legacy's bare number of requests.
/// </remarks>
internal static class ProductNames
{
    private const string ResourceName = "billing-product-names.json";
    private const string DonationKey = "donation";
    private const string PackKey = "pack";
    private const string PlanKey = "plan";
    private const string PlansKey = "plans";
    private const string RequestsSlot = "{{ requests }}";
    private const string PlanSlot = "{{ plan }}";

    private static readonly JsonObject Table = Load();

    public static string Donation(UiLocale locale) => Text(locale, DonationKey);

    public static string Pack(UiLocale locale, ApiPackTerms pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var requests = pack.Requests.ToString(CultureInfo.InvariantCulture);
        return Text(locale, PackKey).Replace(RequestsSlot, requests, StringComparison.Ordinal);
    }

    public static string Plan(UiLocale locale, ApiPlanTerms plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var name = PlanName(UiLocales.Code(locale), plan.Code)
            ?? PlanName(UiLocales.Code(UiLocales.Fallback), plan.Code)
            ?? plan.Code;
        return Text(locale, PlanKey).Replace(PlanSlot, name, StringComparison.Ordinal);
    }

    private static string Text(UiLocale locale, string key) =>
        Value(UiLocales.Code(locale), key)
        ?? Value(UiLocales.Code(UiLocales.Fallback), key)
        ?? throw new InvalidOperationException($"No product name {key} in {ResourceName}.");

    private static string? Value(string localeCode, string key) =>
        Table[localeCode]?[key]?.GetValue<string>();

    private static string? PlanName(string localeCode, string planCode) =>
        Table[localeCode]?[PlansKey]?[planCode]?.GetValue<string>();

    private static JsonObject Load()
    {
        using var stream = typeof(ProductNames).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The resource {ResourceName} is missing.");
        return JsonNode.Parse(stream)?.AsObject()
            ?? throw new InvalidOperationException($"The resource {ResourceName} is empty.");
    }
}
