namespace LoDb.Api.Modules.Seo.IndexNow;

/// <summary>
/// The body of one IndexNow request: the host, the key and where the site publishes it, and
/// at most <see cref="IndexNowClient.MaxUrls"/> URLs of that host.
/// </summary>
/// <remarks>Written camelCase, the names of the protocol's fields.</remarks>
internal sealed record IndexNowSubmission
{
    public required string Host { get; init; }

    public required string Key { get; init; }

    public required string KeyLocation { get; init; }

    public required IReadOnlyList<string> UrlList { get; init; }
}
