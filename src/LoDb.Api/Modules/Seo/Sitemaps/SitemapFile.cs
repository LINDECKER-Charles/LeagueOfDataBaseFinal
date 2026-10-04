using System.Diagnostics.CodeAnalysis;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// The name of a sitemap file below <c>/sitemaps/</c>: <c>latest.xml</c> or
/// <c>{version}.xml</c>, and the paths the site serves them at.
/// </summary>
internal sealed record SitemapFile
{
    /// <summary>Path of the sitemap index.</summary>
    public const string IndexPath = "/sitemap.xml";

    private const string Prefix = "/sitemaps/";
    private const string Extension = ".xml";
    private const string LatestName = "latest";

    private SitemapFile(PatchVersion? version) => Version = version;

    private static SitemapFile Latest { get; } = new(version: null);

    /// <summary>The version the file lists; null for <c>latest.xml</c>.</summary>
    public PatchVersion? Version { get; }

    public bool IsLatest => Version is null;

    /// <summary>Reads a file name; false for anything but the two forms.</summary>
    public static bool TryRead(string? name, [NotNullWhen(true)] out SitemapFile? file)
    {
        file = null;
        if (name is null || !name.EndsWith(Extension, StringComparison.Ordinal))
        {
            return false;
        }

        var stem = name[..^Extension.Length];
        if (stem == LatestName)
        {
            file = Latest;
        }
        else if (PatchVersion.TryParse(stem, out var version))
        {
            file = new SitemapFile(version);
        }

        return file is not null;
    }

    /// <summary>
    /// Path of a locale's sitemap: <paramref name="version"/> null for the latest one.
    /// </summary>
    public static string PathOf(UiLocale locale, PatchVersion? version) =>
        $"{Prefix}{UiLocales.Code(locale)}/{version?.Value ?? LatestName}{Extension}";
}
