using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LoDb.Api.Tests.Prerender.Support;

/// <summary>
/// The SSR server as the pass sees it (the contracts of <c>src/server/prerender</c>): every
/// page a 200 of the <c>archived</c> class, gzip, marked with the build it runs, unless the test
/// scripts other answers for a path; the list of that build and its files.
/// </summary>
internal sealed class FakeSsr : HttpMessageHandler
{
    public const string BuildA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string BuildB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string ArchivedCaching = "public, max-age=3600, s-maxage=31536000";
    public const string Policy = "script-src 'self' 'sha256-cGFnZQ=='";
    public const string ReportOnlyPolicy = "require-trusted-types-for 'script'";
    public const string Endpoints = "csp=\"/api/security/csp-reports\"";
    public const string Preconnect = "<https://ddragon.leagueoflegends.com>; rel=preconnect";
    public const string BuildFilesPath = "/_lodb/build-files";
    public const string BuildFolder = "/build/";

    private readonly ConcurrentDictionary<string, ConcurrentQueue<Func<HttpResponseMessage>>>
        _scripts = new(StringComparer.Ordinal);

    private readonly ConcurrentQueue<SsrRequest> _requests = new();

    /// <summary>The build the SSR runs now: its pages carry it, its list names it.</summary>
    public string Build { get; set; } = BuildA;

    /// <summary>The hashed files of every build.</summary>
    public IReadOnlyList<string> Files { get; } = ["main-ABCD1234.js", "media/font-ABCD1234.woff2"];

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyCollection<SsrRequest> Requests => _requests;

    /// <summary>Called with the path of each page asked for, before it is answered.</summary>
    public Func<string, Task>? OnPage { get; set; }

    /// <summary>
    /// Whether a page answers 301 now, as the latest patch's versioned URLs do: those of a
    /// version still held for the latest one by a cache.
    /// </summary>
    public Func<string, bool>? IsRedirected { get; set; }

    /// <summary>The paths of the pages asked for, in order, the lists and files left out.</summary>
    public IReadOnlyList<string> Pages => [.. _requests
        .Select(static request => request.Path)
        .Where(static path => path != BuildFilesPath
            && !path.StartsWith(BuildFolder, StringComparison.Ordinal))];

    /// <summary>
    /// The next answers to <paramref name="path"/>, one per request, then the page.
    /// </summary>
    public void Answer(string path, params Func<HttpResponseMessage>[] answers)
    {
        var queue = _scripts.GetOrAdd(path, static _ => new());
        foreach (var answer in answers)
        {
            queue.Enqueue(answer);
        }
    }

    /// <summary>A page of the pass: what the SSR stores, as it sends it.</summary>
    public static HttpResponseMessage Archived(string path, string build)
    {
        var response = Html(path, HttpStatusCode.OK);
        response.Headers.CacheControl = CacheControlHeaderValue.Parse(ArchivedCaching);
        response.Headers.TryAddWithoutValidation("X-LoDb-Cache-Class", "archived");
        response.Headers.TryAddWithoutValidation("X-LoDb-Build", build);
        response.Headers.TryAddWithoutValidation("Content-Security-Policy", Policy);
        response.Headers.TryAddWithoutValidation(
            "Content-Security-Policy-Report-Only",
            ReportOnlyPolicy);
        response.Headers.TryAddWithoutValidation("Reporting-Endpoints", Endpoints);
        response.Headers.TryAddWithoutValidation("Link", Preconnect);
        response.Headers.Vary.Add("Accept-Encoding");
        return response;
    }

    /// <summary>An answer of <paramref name="status"/>, with a Retry-After when given.</summary>
    public static HttpResponseMessage Status(HttpStatusCode status, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("—") };
        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.RetryAfter =
                new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        }

        return response;
    }

    /// <summary>A 200 page of another class, gzip, with the given caching.</summary>
    public static HttpResponseMessage Unmarked(string path, string cacheControl)
    {
        var response = Html(path, HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Cache-Control", cacheControl);
        return response;
    }

    /// <summary>The gzip body of the page at <paramref name="path"/>.</summary>
    public static byte[] BodyOf(string path)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes($"<!doctype html><title>{path}</title>"));
        }

        return buffer.ToArray();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        _requests.Enqueue(new SsrRequest(path, request.Headers.ToDictionary(
            static header => header.Key,
            static header => string.Join(',', header.Value),
            StringComparer.OrdinalIgnoreCase)));
        if (path == BuildFilesPath)
        {
            return Listing();
        }

        if (path.StartsWith(BuildFolder, StringComparison.Ordinal))
        {
            return BuildFile(path);
        }

        if (OnPage is { } onPage)
        {
            await onPage(path);
        }

        if (_scripts.TryGetValue(path, out var queue) && queue.TryDequeue(out var answer))
        {
            return answer();
        }

        return IsRedirected?.Invoke(path) == true
            ? Status(HttpStatusCode.MovedPermanently)
            : Archived(path, Build);
    }

    private static HttpResponseMessage Html(string path, HttpStatusCode status)
    {
        var content = new ByteArrayContent(BodyOf(path));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html; charset=utf-8");
        content.Headers.ContentEncoding.Add("gzip");
        return new HttpResponseMessage(status) { Content = content };
    }

    private HttpResponseMessage Listing()
    {
        var json = JsonSerializer.Serialize(new { build = Build, files = Files });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage BuildFile(string path)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes($"/* {path} */"));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/javascript; charset=UTF-8");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}
