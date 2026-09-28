using LoDb.Domain.Catalog.Modes;

namespace LoDb.Api.Modules.Builds.Editing.Bodies;

/// <summary>A build as the editor submits it, to create one or to replace one.</summary>
internal sealed record BuildRequest
{
    /// <summary>3 to 80 characters once trimmed.</summary>
    public string? Name { get; init; }

    /// <summary>At most 2000 characters once trimmed; a blank one is no description.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the trends list the build and anyone may vote on it; its link works either way.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// The patch the build is pinned to, one Data Dragon lists; the latest when blank.
    /// </summary>
    public string? GameVersion { get; init; }

    /// <summary>The mode whose map gates the items; Summoner's Rift when absent.</summary>
    public GameMode? GameMode { get; init; }

    /// <summary>
    /// The Data Dragon language the build is written in; the call's <c>lang</c>, else en_US,
    /// when blank.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>The champion, the rune page and the purchase order; required.</summary>
    public StructureBody? Structure { get; init; }
}
