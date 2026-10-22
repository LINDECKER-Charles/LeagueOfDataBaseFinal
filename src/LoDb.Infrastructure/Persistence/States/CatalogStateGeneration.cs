namespace LoDb.Infrastructure.Persistence.States;

/// <summary>
/// A row of <c>catalog_state_generation</c>: one build of the index of entity states under one
/// fingerprint scheme (docs/produit/seo-une-page-par-etat.md, § 3.2).
/// </summary>
/// <remarks>
/// A rebuild writes a new generation next to the current one, then flips
/// <see cref="IsCurrent"/> in one transaction and deletes the old one: the API only reads the
/// current generation of the scheme it compiles, so a rebuild never disturbs the image that
/// serves, nor the image before it on a rollback.
/// </remarks>
public sealed class CatalogStateGeneration
{
    /// <summary>The longest game version, as in <c>ddragon_version</c>.</summary>
    public const int VersionMaxLength = 32;

    public long Id { get; set; }

    /// <summary>
    /// The fingerprint scheme its states were computed with (<c>FingerprintScheme</c>).
    /// </summary>
    public int Scheme { get; set; }

    /// <summary>
    /// The coverage start: the oldest listed version of the run of stored en_US catalogs that
    /// ends at the latest promoted version. A start that moves calls for a new generation.
    /// </summary>
    public required string FirstVersion { get; set; }

    /// <summary>
    /// The coverage end, the newest version indexed so far; null until the first one is. With
    /// <see cref="Id"/>, the stamp a reader compares to know whether its copy is stale.
    /// </summary>
    public string? LastVersion { get; set; }

    /// <summary>
    /// When the generation was started: the date a rebuilt history is dated by, where no
    /// promotion dates it (sitemaps' <c>lastmod</c>).
    /// </summary>
    public DateTimeOffset BuiltAt { get; set; }

    /// <summary>Rewritten with each version indexed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The generation readers use; one at most per scheme.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>States of the generation, open and closed: the summary line's count.</summary>
    public int StateCount { get; set; }

    /// <summary>States still open, which the coverage end extends.</summary>
    public int OpenStateCount { get; set; }
}
