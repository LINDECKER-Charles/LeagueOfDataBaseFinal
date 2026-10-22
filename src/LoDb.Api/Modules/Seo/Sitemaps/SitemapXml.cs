using System.Globalization;
using System.Text;
using System.Xml;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>
/// Writes the two documents of the sitemaps protocol: a <c>sitemapindex</c> and a
/// <c>urlset</c>, in UTF-8, one <c>loc</c> per entry and a <c>lastmod</c> when its date is
/// known.
/// </summary>
/// <remarks>
/// Dates are known moments only (<see cref="SitemapEntry"/>), never a guessed release date:
/// a guessed one is worse than none. The writer escapes every value, so a location can never
/// break the document.
/// </remarks>
internal static class SitemapXml
{
    public const string ContentType = "application/xml; charset=utf-8";

    private const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    // W3C Datetime in UTC, the form the protocol recommends.
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = false,
    };

    /// <summary>A sitemap index listing <paramref name="sitemaps"/>.</summary>
    public static byte[] Index(IEnumerable<SitemapEntry> sitemaps) =>
        Write("sitemapindex", "sitemap", sitemaps);

    /// <summary>A sitemap listing <paramref name="pages"/>.</summary>
    public static byte[] UrlSet(IEnumerable<SitemapEntry> pages) => Write("urlset", "url", pages);

    private static byte[] Write(string root, string entry, IEnumerable<SitemapEntry> entries)
    {
        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, Settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement(root, Namespace);
            foreach (var item in entries)
            {
                WriteEntry(writer, entry, item);
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToArray();
    }

    private static void WriteEntry(XmlWriter writer, string entry, SitemapEntry item)
    {
        writer.WriteStartElement(entry, Namespace);
        writer.WriteElementString("loc", Namespace, item.Location);
        if (item.LastModified is { } date)
        {
            var text = date.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
            writer.WriteElementString("lastmod", Namespace, text);
        }

        writer.WriteEndElement();
    }
}
