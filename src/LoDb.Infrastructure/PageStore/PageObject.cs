namespace LoDb.Infrastructure.PageStore;

/// <summary>
/// An object to write: its key in the bucket, its body as it will be served, and the headers
/// the store keeps and gives back with it.
/// </summary>
public sealed record PageObject
{
    /// <summary>
    /// Key in the bucket, without leading slash: <c>pages/{epoch}/{locale}/{version}/…</c> or
    /// <c>build/{name}</c>. Each segment is sent percent-encoded.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>The body, already compressed when <see cref="ContentEncoding"/> says so.</summary>
    public required ReadOnlyMemory<byte> Body { get; init; }

    public required string ContentType { get; init; }

    /// <summary>Stored as is: the store never decompresses (<c>gzip</c> for a page).</summary>
    public string? ContentEncoding { get; init; }

    public string? CacheControl { get; init; }

    public PageStorageClass StorageClass { get; init; } = PageStorageClass.Standard;

    /// <summary>
    /// User metadata, sent as <c>x-amz-meta-{name}</c>: lower-case names without the prefix
    /// (<c>content-security-policy</c>, <c>link</c>…), ASCII values. Their prefixed names and
    /// values weigh 2 KB at most in all, the S3 limit counted strictly; beyond, the write is
    /// refused before it is sent.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Writes only if the key is free (<c>If-None-Match: *</c>): a build file, identical
    /// under its hashed name, is never written twice.
    /// </summary>
    public bool IsCreateOnly { get; init; }
}
