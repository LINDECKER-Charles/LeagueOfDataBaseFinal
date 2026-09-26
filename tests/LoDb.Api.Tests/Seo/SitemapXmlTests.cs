using System.Text;
using System.Xml.Linq;
using LoDb.Api.Modules.Seo.Sitemaps;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The sitemap documents: UTF-8 without a byte order mark, in the sitemaps.org namespace,
/// a <c>loc</c> per URL and never a <c>lastmod</c>.
/// </summary>
public sealed class SitemapXmlTests
{
    internal static readonly XNamespace Sitemaps = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static readonly string[] Urls =
    [
        "https://league-of-data-base.com/en/",
        "https://league-of-data-base.com/en/items/1004-faerie-charm?a=1&b=2",
    ];

    [Fact]
    public void WritesAnIndexOfSitemaps()
    {
        var document = Parse(SitemapXml.Index(Urls));

        Assert.Equal(Sitemaps + "sitemapindex", document.Root!.Name);
        Assert.All(document.Root.Elements(), static entry =>
            Assert.Equal(Sitemaps + "sitemap", entry.Name));
        Assert.Equal(Urls, Locations(document));
    }

    [Fact]
    public void WritesASetOfUrls()
    {
        var document = Parse(SitemapXml.UrlSet(Urls));

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
        var body = SitemapXml.UrlSet(Urls);
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
