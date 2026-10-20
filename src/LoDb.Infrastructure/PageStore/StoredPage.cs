namespace LoDb.Infrastructure.PageStore;

/// <summary>An object as a HEAD describes it: its size and the headers the store keeps.
/// </summary>
public sealed record StoredPage
{
    /// <summary>The class the store reports when it names none, as S3 does for Standard.</summary>
    public const string StandardStorageClass = "STANDARD";

    public required string Key { get; init; }

    public required long ContentLength { get; init; }

    public string? ContentType { get; init; }

    public string? ContentEncoding { get; init; }

    public string? CacheControl { get; init; }

    /// <summary>The store's name of the class: <c>ONEZONE_IA</c>, <c>STANDARD</c>…</summary>
    public required string StorageClass { get; init; }

    /// <summary>User metadata, by lower-case name without the <c>x-amz-meta-</c> prefix.</summary>
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
