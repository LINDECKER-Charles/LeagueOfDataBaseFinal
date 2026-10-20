using LoDb.Infrastructure.PageStore;

namespace LoDb.Api.Modules.Admin.Prerender.Builds;

/// <summary>
/// A build's file as the store keeps it: under <c>build/</c>, for as long as needed.
/// </summary>
/// <remarks>
/// Standard class, unlike the pages: once <c>front-builds</c> prunes a build, no pass can
/// write its files again. Create-only: a hashed name always holds the same bytes, so a file an
/// earlier build stored is left as it is (412). Cached a year and never revalidated, as the SSR
/// serves it.
/// </remarks>
internal static class BuildObjects
{
    public const string Prefix = "build/";

    private const string ImmutableCaching = "public, max-age=31536000, immutable";
    private const string AnyContent = "application/octet-stream";

    public static PageObject Of(BuildFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new PageObject
        {
            Key = Prefix + file.Name,
            Body = file.Body,
            ContentType = file.ContentType ?? AnyContent,
            ContentEncoding = file.ContentEncoding,
            CacheControl = ImmutableCaching,
            StorageClass = PageStorageClass.Standard,
            IsCreateOnly = true,
        };
    }
}
