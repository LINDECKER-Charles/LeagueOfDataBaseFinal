using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Legacy.Paths;

/// <summary>An old URL's path, read: what it showed and what it named, not checked yet.</summary>
internal sealed record LegacyPath
{
    private LegacyPath(LegacyPathKind kind) => Kind = kind;

    public LegacyPathKind Kind { get; }

    /// <summary>
    /// For a page, its new path below the locale, without leading slash ("about/data",
    /// "account/login"); empty for the home page.
    /// </summary>
    public string PagePath { get; private init; } = string.Empty;

    /// <summary>For a list or a detail, the resource it showed.</summary>
    public ResourceType Resource { get; private init; }

    /// <summary>For a detail, the old <c>{name}</c> segment: the Data Dragon key.</summary>
    public string Name { get; private init; } = string.Empty;

    /// <summary>The version segment of a <c>/{version}/…</c> path, as written.</summary>
    public string? Version { get; private init; }

    public static LegacyPath Page(string pagePath) =>
        new(LegacyPathKind.Page) { PagePath = pagePath };

    public static LegacyPath List(ResourceType resource, string? version) =>
        new(LegacyPathKind.List) { Resource = resource, Version = version };

    public static LegacyPath Detail(ResourceType resource, string name, string? version) =>
        new(LegacyPathKind.Detail) { Resource = resource, Name = name, Version = version };
}
