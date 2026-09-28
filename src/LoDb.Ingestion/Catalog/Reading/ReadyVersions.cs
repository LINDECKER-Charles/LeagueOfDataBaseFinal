using System.ComponentModel;
using System.Text.Json.Serialization;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// The ready versions and the latest one, as the <c>HybridCache</c> holds them.
/// </summary>
/// <remarks>
/// Sealed and marked immutable, so the cache hands out this instance instead of a copy.
/// Codes rather than domain values: the entry must stay JSON-serializable for a second-level
/// cache.
/// </remarks>
[ImmutableObject(true)]
internal sealed class ReadyVersions
{
    [JsonConstructor]
    public ReadyVersions(IReadOnlyList<string> codes, string? latest)
    {
        ArgumentNullException.ThrowIfNull(codes);
        Codes = codes;
        Latest = latest;
    }

    /// <summary>The ready versions, newest first.</summary>
    public IReadOnlyList<string> Codes { get; }

    /// <summary>The newest promoted version, if any.</summary>
    public string? Latest { get; }
}
