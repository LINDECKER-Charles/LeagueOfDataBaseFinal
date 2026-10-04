using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Billing.Http;

/// <summary>The locale a buyer pays in and comes back to.</summary>
internal static class BillingLocale
{
    /// <summary>The locale of <paramref name="code"/>, as <c>fr</c>; English if unknown.</summary>
    public static UiLocale Of(string? code) =>
        UiLocales.TryParse(code?.Trim(), out var locale) ? locale : UiLocales.Fallback;
}
