using System.Net;
using System.Net.Http.Headers;
using LoDb.Api.Modules.Admin.Prerender.Builds;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>
/// The pass's calls to the SSR server, on the stack network: a page, the list of the build it
/// runs, a file of that build.
/// </summary>
/// <remarks>
/// Through the <c>prerender-ssr</c> client (<see cref="PrerenderRegistration"/>): every request
/// carries the pass's marker, the public host and scheme, and asks for gzip; nothing is
/// decompressed and no redirect is followed, so that a 301 is seen as one. A timeout or a
/// transport failure is a <see cref="SsrUnavailableException"/>; a cancellation by the caller
/// stays one.
/// </remarks>
internal sealed class SsrClient(IHttpClientFactory clients, TimeProvider clock)
{
    public const string ClientName = "prerender-ssr";

    /// <summary>Where the SSR lists its build's files, to the pass alone.</summary>
    public const string BuildFilesPath = "/_lodb/build-files";

    private const string BuildFolder = "/build/";

    /// <summary>The page at <paramref name="requestPath"/> (<c>/fr/16.19.1/items</c>).</summary>
    public async Task<RenderedPage> RenderAsync(
        string requestPath,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(requestPath, cancellationToken);
        return new RenderedPage
        {
            Status = response.StatusCode,
            Body = await response.Content.ReadAsByteArrayAsync(cancellationToken),
            Headers = HeadersOf(response),
            RetryAfter = RetryDelays.Of(response.Headers.RetryAfter, clock.GetUtcNow()),
        };
    }

    /// <summary>The hashed files of the build the SSR runs now.</summary>
    public async Task<BuildListing> ListBuildAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(BuildFilesPath, cancellationToken);
        EnsureFound(response, BuildFilesPath);
        return BuildListing.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    /// <summary>A file of that build, as the SSR serves it under <c>/build/</c>.</summary>
    public async Task<BuildFile> GetBuildFileAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var path = BuildFolder + name;
        using var response = await SendAsync(path, cancellationToken);
        EnsureFound(response, path);
        var content = response.Content.Headers;
        return new BuildFile
        {
            Name = name,
            Body = await response.Content.ReadAsByteArrayAsync(cancellationToken),
            ContentType = Joined(content, HeaderNames.ContentType),
            ContentEncoding = Joined(content, HeaderNames.ContentEncoding),
        };
    }

    private static void EnsureFound(HttpResponseMessage response, string path)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new SsrUnavailableException(
                $"GET {path} answered {(int)response.StatusCode}.");
        }
    }

    // Every header kept as sent, never parsed and written back: the store must keep the page's
    // Cache-Control and policies exactly as the SSR chose them.
    private static Dictionary<string, string> HeadersOf(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sent = response.Headers.NonValidated.Concat(response.Content.Headers.NonValidated);
        foreach (var (name, values) in sent)
        {
            headers[name] = values.ToString();
        }

        return headers;
    }

    private static string? Joined(HttpHeaders headers, string name) =>
        headers.NonValidated.TryGetValues(name, out var values) ? values.ToString() : null;

    // The answer is read whole, within the client's timeout, so a reset while reading it is a
    // transport failure too.
    private async Task<HttpResponseMessage> SendAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await clients.CreateClient(ClientName).GetAsync(
                new Uri(path, UriKind.Relative),
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new SsrUnavailableException($"GET {path} timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SsrUnavailableException($"GET {path} failed in transport.", exception)
            {
                IsUnreachable = exception.HttpRequestError
                    is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError,
            };
        }
    }
}
