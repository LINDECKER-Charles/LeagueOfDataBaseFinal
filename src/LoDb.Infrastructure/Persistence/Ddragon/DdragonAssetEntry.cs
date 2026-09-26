namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>What an ingestion found for one asset, before it is recorded.</summary>
public sealed record DdragonAssetEntry(
    string Version,
    string Type,
    string Key,
    DdragonAssetStatus Status,
    string? Sha256,
    string? Extension)
{
    public static DdragonAssetEntry Present(
        string version,
        string type,
        string key,
        string sha256,
        string extension) =>
        new(version, type, key, DdragonAssetStatus.Present, sha256, extension);

    /// <summary>A definitive absence (403/404), never a transient error.</summary>
    public static DdragonAssetEntry Absent(string version, string type, string key) =>
        new(version, type, key, DdragonAssetStatus.Absent, null, null);
}
