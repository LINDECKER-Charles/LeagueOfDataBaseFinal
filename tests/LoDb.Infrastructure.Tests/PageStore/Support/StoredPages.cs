using System.IO.Compression;
using System.Text;
using LoDb.Infrastructure.PageStore;

namespace LoDb.Infrastructure.Tests.PageStore.Support;

/// <summary>Objects as the pass writes them: a page of an old patch and a build file.</summary>
internal static class StoredPages
{
    public const string HtmlType = "text/html; charset=utf-8";
    public const string ArchivedCacheControl = "public, max-age=0, s-maxage=31536000";

    /// <summary>A gzip page under <c>pages/{epoch}/…</c>, in One Zone, with metadata.</summary>
    public static PageObject Page(string key, IReadOnlyDictionary<string, string> metadata) =>
        new()
        {
            Key = key,
            Body = Gzip("<!doctype html><html lang=\"fr\"><title>Bottes</title></html>"),
            ContentType = HtmlType,
            ContentEncoding = "gzip",
            CacheControl = ArchivedCacheControl,
            StorageClass = PageStorageClass.OneZone,
            Metadata = metadata,
        };

    /// <summary>A hashed build file under <c>build/</c>, written once, kept in Standard.</summary>
    public static PageObject BuildFile(string key, string body) =>
        new()
        {
            Key = key,
            Body = Encoding.UTF8.GetBytes(body),
            ContentType = "text/javascript",
            CacheControl = "public, max-age=31536000, immutable",
            IsCreateOnly = true,
        };

    private static byte[] Gzip(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }

        return output.ToArray();
    }
}
