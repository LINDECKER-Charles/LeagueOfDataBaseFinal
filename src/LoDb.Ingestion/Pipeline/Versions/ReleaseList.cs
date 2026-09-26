using System.Collections.Frozen;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>
/// A list Data Dragon publishes (versions or languages), as the <c>HybridCache</c> holds it.
/// </summary>
/// <remarks>
/// Sealed and marked immutable, so the cache hands out this instance instead of a copy.
/// Codes rather than domain values: the entry must stay JSON-serializable for a second-level
/// cache.
/// </remarks>
[ImmutableObject(true)]
internal sealed class ReleaseList
{
    private readonly FrozenSet<string> lookup;

    [JsonConstructor]
    public ReleaseList(IReadOnlyList<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        Codes = codes;
        lookup = codes.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>The codes in the upstream order: newest version first.</summary>
    public IReadOnlyList<string> Codes { get; }

    public bool Contains(string code) => lookup.Contains(code);
}
