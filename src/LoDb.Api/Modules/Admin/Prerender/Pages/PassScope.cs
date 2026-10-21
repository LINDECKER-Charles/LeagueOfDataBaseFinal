using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The perimeter a pass was launched over: explicit lists of older patches, locales and types
/// of page, each left null for all of them, the versions to render first, and whether the
/// units already complete in its epoch were left out.
/// </summary>
/// <remarks>
/// Stored as JSON in <c>prerender_pass.scope</c> (<see cref="PassScopeJson"/>). A pass stored
/// before the lists (October 2026) may hold a range instead: <see cref="Oldest"/> and
/// <see cref="Newest"/> are read, never written by the admin.
/// </remarks>
internal sealed record PassScope
{
    /// <summary>Every older patch, every page, every locale, newest first.</summary>
    public static PassScope Everything { get; } = new();

    /// <summary>The older patches rendered; null for every one of them.</summary>
    public IReadOnlyList<PatchVersion>? Versions { get; init; }

    /// <summary>The oldest version of a range stored before the lists; null otherwise.</summary>
    public PatchVersion? Oldest { get; init; }

    /// <summary>The newest version of a range stored before the lists; null otherwise.</summary>
    public PatchVersion? Newest { get; init; }

    /// <summary>The locales rendered, in the site's order; null for all of them.</summary>
    public IReadOnlyList<UiLocale>? Locales { get; init; }

    /// <summary>The types of page rendered, their lists and their entities; null for all.</summary>
    public IReadOnlyList<ResourceType>? PageTypes { get; init; }

    /// <summary>
    /// The versions to render first, in this order; the rest follow, newest first.
    /// </summary>
    public IReadOnlyList<PatchVersion> Priority { get; init; } = [];

    /// <summary>Whether the units already complete in the pass's epoch were left out.</summary>
    public bool SkipComplete { get; init; }

    /// <summary>Whether it renders every type of page: a promotion may then join it.</summary>
    public bool HasEveryPageType => PageTypes is null;

    /// <summary>Whether nothing restricts it: its epoch is then whole once it completes.</summary>
    public bool IsWhole =>
        Versions is null
        && Oldest is null
        && Newest is null
        && Locales is null
        && PageTypes is null;

    /// <summary>A scope of exactly one version, as a promotion opens.</summary>
    public static PassScope Of(PatchVersion version) => new() { Versions = [version] };

    public bool Admits(PatchVersion version) =>
        (Versions is null || Versions.Contains(version))
        && (Oldest is null || version >= Oldest)
        && (Newest is null || version <= Newest);

    public bool Admits(UiLocale locale) => Locales is null || Locales.Contains(locale);
}
