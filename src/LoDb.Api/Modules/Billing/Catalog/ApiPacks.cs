namespace LoDb.Api.Modules.Billing.Catalog;

/// <summary>The credit packs on sale: one euro cent a request.</summary>
internal static class ApiPacks
{
    /// <summary>Every pack, from the smallest.</summary>
    public static IReadOnlyList<ApiPackTerms> All { get; } =
    [
        new() { Code = "small", PriceCents = 500, Requests = 5_000 },
        new() { Code = "medium", PriceCents = 1_000, Requests = 10_000 },
        new() { Code = "large", PriceCents = 2_000, Requests = 20_000 },
    ];

    /// <summary>The pack named <paramref name="code"/>; null for any other code.</summary>
    public static ApiPackTerms? Find(string? code) =>
        All.FirstOrDefault(pack => pack.Code.Equals(code, StringComparison.Ordinal));
}
