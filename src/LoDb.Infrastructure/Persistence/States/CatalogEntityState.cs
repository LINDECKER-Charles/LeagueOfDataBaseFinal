using LoDb.Domain.Catalog;

namespace LoDb.Infrastructure.Persistence.States;

/// <summary>
/// A row of <c>catalog_entity_state</c>: a run of consecutive covered versions over which an
/// entity's displayed projection keeps one fingerprint (docs/produit/seo-une-page-par-etat.md,
/// § 3.2). Its first version, the opener, is the page search engines index.
/// </summary>
/// <remarks>
/// An open state has neither <see cref="LastVersion"/> nor <see cref="ClosedAt"/>: a read
/// substitutes the coverage end, so a promotion that extends the index rewrites no open row.
/// </remarks>
public sealed class CatalogEntityState
{
    /// <summary>The longest entity key: ids are far shorter (<c>summonerflash_jade</c>).</summary>
    public const int EntityKeyMaxLength = 64;

    /// <summary>A SHA-256 in lowercase hexadecimal.</summary>
    public const int FingerprintLength = 64;

    /// <summary>Far over the longest canonical path, as <c>prerender_demand</c> has it.</summary>
    public const int PathMaxLength = 160;

    public long Id { get; set; }

    public long GenerationId { get; set; }

    /// <summary>Stored as the first segment of its paths (<c>items</c>).</summary>
    public required ResourceType Resource { get; set; }

    /// <summary>The entity's id, case folded (<c>EntityKey</c> of the domain).</summary>
    public required string EntityKey { get; set; }

    public required string OpenerVersion { get; set; }

    /// <summary>The last version of a closed state; null while open.</summary>
    public string? LastVersion { get; set; }

    /// <summary>When the state was closed; null while open.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    public required string Fingerprint { get; set; }

    /// <summary>
    /// The entity's canonical path in the opener version (<c>items/3371-molten-edge</c>): the
    /// slug and the case of the id there.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>The opener of the entity's previous state; null for a new entity.</summary>
    public string? PreviousOpenerVersion { get; set; }

    /// <summary>Whether the entity comes back after versions without it.</summary>
    public bool IsReturn { get; set; }

    /// <summary>
    /// The dotted keys (<c>FieldKey</c>) of the en_US fields that differ from the previous
    /// state's last version; empty for a new entity.
    /// </summary>
    public required IReadOnlyList<string> ChangedFields { get; set; }
}
