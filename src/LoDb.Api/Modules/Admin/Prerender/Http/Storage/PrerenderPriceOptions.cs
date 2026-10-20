using LoDb.Api.Modules.Admin.Prerender.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>
/// The prices the admin multiplies what the page store holds by to estimate what it costs
/// each month (<c>LoDb:Prerender</c>, beside the pass's own settings).
/// </summary>
/// <remarks>
/// The defaults are Scaleway's list prices for Object Storage in the Paris region, excluding
/// VAT, after the update of 2026-06-01, per GB and per hour, over the 730 hours of a month:
/// 0.000011 € in Standard One Zone, the class of every page, and 0.000022 € in Standard
/// Multi-AZ, the class of the bundles. Requests and uploads are free; the first 75 GB
/// downloaded each month too, then 0.01 € per GB: the egress is left out of the estimate and
/// followed in Grafana.
/// </remarks>
internal sealed class PrerenderPriceOptions
{
    public const string SectionName = PrerenderOptions.SectionName;

    /// <summary>Scaleway, Standard One Zone, Paris, per GB and per month.</summary>
    public const decimal ScalewayOneZonePricePerGbMonth = 0.00803m;

    /// <summary>Scaleway, Standard Multi-AZ, Paris, per GB and per month.</summary>
    public const decimal ScalewayMultiAzPricePerGbMonth = 0.01606m;

    /// <summary>The currency of both prices, ISO 4217.</summary>
    public const string Euro = "EUR";

    /// <summary>Price of one GB (10^9 bytes) of pages stored for a month.</summary>
    public decimal PricePerGbMonthOneZone { get; set; } = ScalewayOneZonePricePerGbMonth;

    /// <summary>Price of one GB (10^9 bytes) of bundles stored for a month.</summary>
    public decimal PricePerGbMonthMultiAz { get; set; } = ScalewayMultiAzPricePerGbMonth;
}
