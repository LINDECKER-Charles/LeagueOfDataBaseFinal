namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>
/// A row of <c>ddragon_asset</c>, the image manifest (ADR 0004): what one (version, type,
/// key) resolves to.
/// </summary>
/// <remarks>
/// A key missing from the table was never tried. A transient error is never written.
/// </remarks>
public sealed class DdragonAsset
{
    public required string Version { get; set; }

    /// <summary>Kind of image, such as <c>champion</c> or <c>item</c> (set by lot 1).</summary>
    public required string Type { get; set; }

    public required string Key { get; set; }

    public DdragonAssetStatus Status { get; set; }

    /// <summary>Lowercase hexadecimal SHA-256 of the blob; null when absent.</summary>
    public string? Sha256 { get; set; }

    /// <summary>Extension of the blob, without dot; null when absent.</summary>
    public string? Extension { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}
