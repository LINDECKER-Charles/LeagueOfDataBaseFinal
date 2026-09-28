namespace LoDb.Api.Modules.Billing.Catalog;

/// <summary>A credit pack of the public API, the legacy <c>ApiCreditPack</c>.</summary>
internal sealed record ApiPackTerms
{
    /// <summary><c>small</c>, <c>medium</c> or <c>large</c>, as the portal posts it.</summary>
    public required string Code { get; init; }

    /// <summary>Price, in euro cents.</summary>
    public required long PriceCents { get; init; }

    /// <summary>Requests added to the balance of the key, valid twelve months.</summary>
    public required long Requests { get; init; }
}
