using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The perimeter a pass was launched over: a range of older patches, one type of page, one
/// locale, each left open for all of them, and the versions to render first.
/// </summary>
/// <remarks>
/// Stored as JSON in <c>prerender_pass.scope</c> (<see cref="PassScopeJson"/>). The units hold
/// the versions and locales the pass renders; the scope keeps the type of page each of them
/// renders, and what the operator asked for.
/// </remarks>
internal sealed record PassScope
{
    /// <summary>Every older patch, every page, every locale, newest first.</summary>
    public static PassScope Everything { get; } = new();

    /// <summary>The oldest version rendered, included; null for no lower bound.</summary>
    public PatchVersion? Oldest { get; init; }

    /// <summary>The newest version rendered, included; null for no upper bound.</summary>
    public PatchVersion? Newest { get; init; }

    /// <summary>The one type of page rendered, its list and its entities; null for all.</summary>
    public ResourceType? PageType { get; init; }

    /// <summary>The one locale rendered; null for all of them.</summary>
    public UiLocale? Locale { get; init; }

    /// <summary>
    /// The versions to render first, in this order: the most requested ones, which the operator
    /// reads from the edge's log. The rest follow, newest first.
    /// </summary>
    public IReadOnlyList<PatchVersion> Priority { get; init; } = [];

    /// <summary>A scope of exactly one version, as a promotion opens.</summary>
    public static PassScope Of(PatchVersion version) =>
        new() { Oldest = version, Newest = version };

    public bool Admits(PatchVersion version) =>
        (Oldest is null || version >= Oldest) && (Newest is null || version <= Newest);

    public bool Admits(UiLocale locale) => Locale is null || Locale == locale;
}
