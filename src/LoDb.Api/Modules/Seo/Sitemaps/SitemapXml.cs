using System.Text;
using System.Xml;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// Writes the two documents of the sitemaps protocol: a <c>sitemapindex</c> and a
/// <c>urlset</c>, in UTF-8, one <c>loc</c> per entry.
/// </summary>
/// <remarks>
/// No <c>lastmod</c>: the ingestion dates no version, and a guessed date is worse than none.
/// The writer escapes every value, so a location can never break the document.
/// </remarks>
internal static class SitemapXml
{
    public const string ContentType = "application/xml; charset=utf-8";

    private const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = false,
    };

    /// <summary>A sitemap index listing <paramref name="sitemaps"/>.</summary>
    public static byte[] Index(IEnumerable<string> sitemaps) =>
        Write("sitemapindex", "sitemap", sitemaps);

    /// <summary>A sitemap listing <paramref name="pages"/>.</summary>
    public static byte[] UrlSet(IEnumerable<string> pages) => Write("urlset", "url", pages);

    private static byte[] Write(string root, string entry, IEnumerable<string> locations)
    {
        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, Settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement(root, Namespace);
            foreach (var location in locations)
            {
                writer.WriteStartElement(entry, Namespace);
                writer.WriteElementString("loc", Namespace, location);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToArray();
    }
}
