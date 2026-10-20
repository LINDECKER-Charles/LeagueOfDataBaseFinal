using System.Net.Http.Headers;
using LoDb.Infrastructure.PageStore.Signing;
using Microsoft.Net.Http.Headers;

namespace LoDb.Infrastructure.PageStore.Http;

/// <summary>
/// The requests of the store, path-style (<c>{endpoint}/{bucket}/{key}</c>), with the headers
/// S3 keeps on the object; the signing handler adds the signature on the way out.
/// </summary>
internal static class PageStoreRequests
{
    /// <summary>The prefix S3 gives every user metadata header.</summary>
    public const string MetadataPrefix = "x-amz-meta-";

    /// <summary>The header naming an object's class, on a PUT and in a HEAD's answer.</summary>
    public const string StorageClassHeader = "x-amz-storage-class";

    private const string AnyObject = "*";

    /// <summary>A PUT of <paramref name="page"/>, its headers stored as given.</summary>
    public static HttpRequestMessage Put(PageStoreOptions settings, PageObject page)
    {
        PageObjectRules.Check(page);
        var content = new ReadOnlyMemoryContent(page.Body);
        content.Headers.TryAddWithoutValidation(HeaderNames.ContentType, page.ContentType);
        AddIfSet(content.Headers, HeaderNames.ContentEncoding, page.ContentEncoding);
        var request = new HttpRequestMessage(HttpMethod.Put, ObjectUrl(settings, page.Key))
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation(
            StorageClassHeader,
            ClassName(settings, page.StorageClass));
        AddIfSet(request.Headers, HeaderNames.CacheControl, page.CacheControl);
        AddIfSet(request.Headers, HeaderNames.IfNoneMatch, page.IsCreateOnly ? AnyObject : null);
        foreach (var (name, value) in page.Metadata)
        {
            request.Headers.TryAddWithoutValidation(MetadataPrefix + name, value);
        }

        return request;
    }

    /// <summary>A HEAD of the object under <paramref name="key"/>.</summary>
    public static HttpRequestMessage Head(PageStoreOptions settings, string key)
    {
        PageObjectRules.CheckKey(key);
        return new HttpRequestMessage(HttpMethod.Head, ObjectUrl(settings, key));
    }

    // Each segment escaped by the signature's own rule, so the URL sent and the path signed
    // are the same bytes.
    private static Uri ObjectUrl(PageStoreOptions settings, string key)
    {
        var endpoint = new Uri(settings.Endpoint!, UriKind.Absolute);
        var path = string.Join('/', key.Split('/').Select(SigV4Encoding.Encode));
        return new Uri(
            $"{endpoint.GetLeftPart(UriPartial.Authority)}/{settings.Bucket}/{path}",
            UriKind.Absolute);
    }

    private static string ClassName(PageStoreOptions settings, PageStorageClass storageClass) =>
        storageClass == PageStorageClass.OneZone
            ? settings.OneZoneStorageClass
            : StoredPage.StandardStorageClass;

    private static void AddIfSet(
        HttpHeaders headers,
        string name,
        string? value)
    {
        if (value is not null)
        {
            headers.TryAddWithoutValidation(name, value);
        }
    }
}
