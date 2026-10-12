using System.Text;
using System.Xml.Linq;
using LoDb.Api.Modules.Seo.Sitemaps;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The sitemap documents: UTF-8 without a byte order mark, in the sitemaps.org namespace,
/// a <c>loc</c> per URL and a <c>lastmod</c> only for a dated entry, in UTC.
/// </summary>
public sealed class SitemapXmlTests
{
    internal static readonly XNamespace Sitemaps = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static readonly string[] Urls =
    [
        "https://league-of-data-base.com/en/",
        "https://league-of-data-base.com/en/items/1004-faerie-charm?a=1&b=2",
    ];

    private static readonly SitemapEntry[] Entries =
        [.. Urls.Select(static url => new SitemapEntry(url))];

    [Fact]
    public void WritesAnIndexOfSitemaps()
    {
        var document = Parse(SitemapXml.Index(Entries));

        Assert.Equal(Sitemaps + "sitemapindex", document.Root!.Name);
        Assert.All(document.Root.Elements(), static entry =>
            Assert.Equal(Sitemaps + "sitemap", entry.Name));
        Assert.Equal(Urls, Locations(document));
    }

    [Fact]
    public void WritesASetOfUrls()
    {
        var document = Parse(SitemapXml.UrlSet(Entries));

        Assert.Equal(Sitemaps + "urlset", document.Root!.Name);
        Assert.All(document.Root.Elements(), static entry =>
        {
            Assert.Equal(Sitemaps + "url", entry.Name);
            Assert.Equal([Sitemaps + "loc"], entry.Elements().Select(static child => child.Name));
        });
        Assert.Equal(Urls, Locations(document));
    }

    [Fact]
    public void EscapesTheUrlsAndStartsWithTheDeclaration()
    {
        var body = SitemapXml.UrlSet(Entries);
        var text = Encoding.UTF8.GetString(body);

        Assert.NotEqual(0xEF, body[0]);
        Assert.StartsWith(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            text,
            StringComparison.Ordinal);
        Assert.Contains("?a=1&amp;b=2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("lastmod", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DatesOnlyTheEntriesThatCarryADateInUtc()
    {
        var promoted = new DateTimeOffset(2026, 10, 7, 1, 30, 5, TimeSpan.FromHours(2));
        SitemapEntry[] entries = [new(Urls[0], promoted), new(Urls[1])];

        var document = Parse(SitemapXml.UrlSet(entries));

        var dates = document.Root!.Elements()
            .Select(static url => url.Element(Sitemaps + "lastmod")?.Value)
            .ToList();
        Assert.Equal(["2026-10-06T23:30:05Z", null], dates);
        Assert.Equal(
            [Sitemaps + "loc", Sitemaps + "lastmod"],
            document.Root.Elements().First().Elements().Select(static child => child.Name));
    }

    [Fact]
    public void WritesAnEmptySet()
    {
        var document = Parse(SitemapXml.UrlSet([]));

        Assert.Empty(document.Root!.Elements());
    }

    internal static XDocument Parse(byte[] body)
    {
        using var stream = new MemoryStream(body);
        return XDocument.Load(stream);
    }

    internal static IReadOnlyList<string> Locations(XDocument document) =>
        [.. document.Descendants(Sitemaps + "loc").Select(static loc => loc.Value)];
}
