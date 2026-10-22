using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.States;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// The state of the entity that holds the page's version: the run of versions over
/// which its page shows the same thing, and the version that opened it.
/// </summary>
internal sealed record EntityState
{
    /// <summary>The version that opened the state, whose page search engines index.</summary>
    public required string OpenerVersion { get; init; }

    /// <summary>
    /// The last version of a closed state; null while it is open. An open state's range never
    /// names the latest version (« depuis le patch X »): a promotion changes nothing of it.
    /// </summary>
    public required string? LastVersion { get; init; }

    public required bool IsOpen { get; init; }

    /// <summary>Whether the page's version is the opener.</summary>
    public required bool IsOpener { get; init; }

    /// <summary>
    /// How the state began; baseline for an entity's first state at the start of the index's
    /// coverage, compared to nothing.
    /// </summary>
    public required EntityChangeKind Kind { get; init; }

    /// <summary>The fields the opener changed; none for a new entity.</summary>
    public required IReadOnlyList<ChangedField> ChangedFields { get; init; }

    /// <summary>
    /// The opener's page, which « Inchangé depuis le patch X » links: its versioned URL, or the
    /// short URL when the opener is the latest version.
    /// </summary>
    public required CanonicalLink OpenerLink { get; init; }

    /// <summary>The state's canonical: its opener once closed, the short URL while open.</summary>
    public required CanonicalLink Canonical { get; init; }
}
