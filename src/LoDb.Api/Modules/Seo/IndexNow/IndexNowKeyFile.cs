namespace LoDb.Api.Modules.Seo.IndexNow;

/// <summary>
/// The file that proves the site holds its IndexNow key: <c>/{key}.txt</c> at the root,
/// which covers every URL of the host, the key as its only content.
/// </summary>
internal static class IndexNowKeyFile
{
    /// <summary>The route of the file; nginx sends such a name to the API (seo.conf).</summary>
    public const string RouteTemplate = "/{key}.txt";

    public const string ContentType = "text/plain; charset=utf-8";

    /// <summary>The path of the file of <paramref name="key"/>.</summary>
    public static string PathOf(string key) => $"/{key}.txt";
}
