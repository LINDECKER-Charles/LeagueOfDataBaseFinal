namespace LoDb.Api.Modules.Admin.Backups;

/// <summary>
/// Settings of the backups panel (<c>LoDb:Backups</c>): the price the admin multiplies the
/// size of the repository by to estimate what the backups cost each month.
/// </summary>
/// <remarks>
/// The defaults are Scaleway's list price for Object Storage, Standard Multi-AZ class, Paris
/// region, excluding VAT, read in October 2026 (scaleway.com/fr/tarifs/storage):
/// 0.000022 € per GB and per hour, over the 730 hours of a month. Requests and uploads are
/// free; the first 75 GB downloaded each month too, which a monthly restore test stays under.
/// </remarks>
internal sealed class BackupOptions
{
    public const string SectionName = "LoDb:Backups";

    /// <summary>Scaleway, Standard Multi-AZ, Paris, per GB and per month.</summary>
    public const decimal ScalewayMultiAzPricePerGbMonth = 0.01606m;

    /// <summary>The currency of <see cref="ScalewayMultiAzPricePerGbMonth"/>.</summary>
    public const string Euro = "EUR";

    /// <summary>Price of one GB (10^9 bytes) stored for a month.</summary>
    public decimal PricePerGbMonth { get; set; } = ScalewayMultiAzPricePerGbMonth;

    /// <summary>ISO 4217 code of <see cref="PricePerGbMonth"/>.</summary>
    public string Currency { get; set; } = Euro;
}
