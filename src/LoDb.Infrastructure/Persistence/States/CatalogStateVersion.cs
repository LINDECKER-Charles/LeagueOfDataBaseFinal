namespace LoDb.Infrastructure.Persistence.States;

/// <summary>
/// A row of <c>catalog_state_version</c>: a listed version a generation covers, written in the
/// transaction that indexes it.
/// </summary>
/// <remarks>
/// The covered versions are the readers' order of versions, which no Data Dragon list read
/// later can contradict, and the point a build interrupted resumes from. The ordinal gives SQL
/// the order the version strings cannot: a statement sorts or ranges states by joining it.
/// </remarks>
public sealed class CatalogStateVersion
{
    public long GenerationId { get; set; }

    public required string Version { get; set; }

    /// <summary>The position of the version in the coverage, 0 for its start.</summary>
    public int Ordinal { get; set; }

    /// <summary>When the version was indexed in this generation.</summary>
    public DateTimeOffset IndexedAt { get; set; }
}
