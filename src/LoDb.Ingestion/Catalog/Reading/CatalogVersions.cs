using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>The versions a client may browse.</summary>
public sealed record CatalogVersions
{
    /// <summary>
    /// The newest promoted version, the one the site and the apps open on;
    /// <see langword="null"/> until the first ingestion completes.
    /// </summary>
    public PatchVersion? Latest { get; init; }

    /// <summary>Every version Data Dragon lists, newest first, the long tail included.</summary>
    public required IReadOnlyList<PatchVersion> Listed { get; init; }

    /// <summary>The versions ingested ahead of any visit, newest first.</summary>
    public required IReadOnlyList<PatchVersion> Ready { get; init; }
}
