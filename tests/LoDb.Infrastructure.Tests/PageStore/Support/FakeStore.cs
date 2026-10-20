using LoDb.Infrastructure.PageStore;

namespace LoDb.Infrastructure.Tests.PageStore.Support;

/// <summary>
/// The wire of the <c>page-store</c> client: each request is kept and answered by
/// <c>answer</c>, which may also throw or wait.
/// </summary>
internal sealed class FakeStore(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
    : HttpMessageHandler
{
    public const string Endpoint = "https://s3.fr-par.scw.cloud";
    public const string Bucket = "lodb-pages-test";
    public const string AccessKey = "SCWFAKEACCESSKEY0000";
    public const string SecretKey = "fake-secret-0f6c3d2a";

    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    /// <summary>Settings that point the store at this fake, with the given changes.</summary>
    public static Dictionary<string, string?> Settings(params (string Key, string? Value)[] changes)
    {
        var settings = new Dictionary<string, string?>
        {
            [Key(nameof(PageStoreOptions.Endpoint))] = Endpoint,
            [Key(nameof(PageStoreOptions.Bucket))] = Bucket,
            [Key(nameof(PageStoreOptions.AccessKey))] = AccessKey,
            [Key(nameof(PageStoreOptions.SecretKey))] = SecretKey,
        };
        foreach (var (key, value) in changes)
        {
            settings[Key(key)] = value;
        }

        return settings;
    }

    private static string Key(string setting) => $"{PageStoreOptions.SectionName}:{setting}";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return answer(request, cancellationToken);
    }
}
