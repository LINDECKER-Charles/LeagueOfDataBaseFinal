using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LoDb.Infrastructure.Storage.Blobs;

/// <summary>
/// Address of a blob: the SHA-256 of its bytes and its file extension.
/// </summary>
/// <remarks>
/// Identical bytes get the same key whatever their version or source name, so an image
/// shared by many patches is stored once. The key is what the asset index persists; every
/// path is derived from it.
/// </remarks>
public sealed partial record BlobKey
{
    private const char ExtensionDot = '.';

    /// <param name="sha256">Lowercase hexadecimal SHA-256 of the bytes.</param>
    /// <param name="extension">Lowercase extension without the dot, such as <c>png</c>.</param>
    public BlobKey(string sha256, string extension)
    {
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentNullException.ThrowIfNull(extension);
        if (!Sha256Pattern().IsMatch(sha256))
        {
            throw new ArgumentException(
                $"'{sha256}' is not a lowercase SHA-256.",
                nameof(sha256));
        }

        if (!ExtensionPattern().IsMatch(extension))
        {
            throw new ArgumentException(
                $"'{extension}' is not a blob extension.",
                nameof(extension));
        }

        Sha256 = sha256;
        Extension = extension;
    }

    public string Sha256 { get; }

    public string Extension { get; }

    /// <summary><c>{sha256}.{ext}</c>.</summary>
    public string FileName => $"{Sha256}{ExtensionDot}{Extension}";

    /// <summary><c>blobs/{sha256}.{ext}</c>, relative to the storage root.</summary>
    public string RelativePath => $"{StorageLayout.BlobsDirectory}/{FileName}";

    /// <summary><c>/cdn/blobs/{sha256}.{ext}</c>, the URL nginx serves.</summary>
    public string PublicPath => StorageLayout.PublicBlobsPrefix + FileName;

    /// <summary>Whether the blob can have a WebP sibling (see <see cref="WebpSibling"/>).</summary>
    public bool HasWebpSlot => WebpSibling.Of(FileName) is not null;

    /// <summary><c>blobs/{sha256}.webp</c>, or <c>null</c> without a WebP slot.</summary>
    public string? WebpRelativePath => WebpSibling.Of(RelativePath);

    /// <summary><c>/cdn/blobs/{sha256}.webp</c>, or <c>null</c> without a WebP slot.</summary>
    public string? WebpPublicPath => WebpSibling.Of(PublicPath);

    /// <summary>Key of the given bytes.</summary>
    /// <param name="content">The blob bytes.</param>
    /// <param name="extension">Extension in any case, with or without a leading dot.</param>
    public static BlobKey ForContent(ReadOnlySpan<byte> content, string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        var normalized = extension.TrimStart(ExtensionDot).ToLowerInvariant();
        return new BlobKey(sha256, normalized);
    }

    [GeneratedRegex(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex(@"^[a-z0-9]{1,10}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionPattern();
}
