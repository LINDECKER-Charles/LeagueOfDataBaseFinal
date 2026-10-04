using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace LoDb.Testing.Fixtures;

/// <summary>
/// Answers the egress client from the recording, never from the network.
/// </summary>
/// <remarks>
/// A recorded 200 replays its body and content type, a recorded 403/404 its status. Any other
/// URL throws <see cref="UnrecordedFixtureException"/>: a test cannot silently depend on an
/// answer nobody recorded. <see cref="FailWith"/> injects the failures a recording cannot
/// hold (5xx, 429).
/// </remarks>
public sealed class FixtureReplayHandler : HttpMessageHandler
{
    private const char UrlSeparator = '/';
    private const StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries;

    private readonly Dictionary<string, RecordedResponse> recorded;
    private readonly string directory;
    private readonly ConcurrentQueue<Uri> requests = new();
    private readonly ConcurrentQueue<Fault> faults = new();

    public FixtureReplayHandler(FixtureIndex index, string directory)
    {
        ArgumentNullException.ThrowIfNull(index);
        recorded = index.Responses.ToDictionary(
            static response => new Uri(response.Url).AbsoluteUri,
            StringComparer.Ordinal);
        this.directory = directory;
    }

    /// <summary>Every request received, in arrival order, retries included.</summary>
    public IReadOnlyList<Uri> Requests => [.. requests];

    /// <summary>
    /// Answers <paramref name="status"/> instead of the recording to every matching request,
    /// from now on.
    /// </summary>
    public void FailWith(Func<Uri, bool> matches, HttpStatusCode status)
    {
        ArgumentNullException.ThrowIfNull(matches);
        faults.Enqueue(new Fault(matches, status));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var url = request.RequestUri
            ?? throw new ArgumentException("The request has no URI.", nameof(request));
        requests.Enqueue(url);
        if (faults.FirstOrDefault(fault => fault.Matches(url)) is { } injected)
        {
            return new HttpResponseMessage(injected.Status) { RequestMessage = request };
        }

        if (!recorded.TryGetValue(url.AbsoluteUri, out var response))
        {
            throw new UnrecordedFixtureException(url);
        }

        return new HttpResponseMessage((HttpStatusCode)response.Status)
        {
            RequestMessage = request,
            Content = await ContentAsync(url, response, cancellationToken).ConfigureAwait(false),
        };
    }

    private async Task<HttpContent> ContentAsync(
        Uri url,
        RecordedResponse response,
        CancellationToken cancellationToken)
    {
        var body = response.Status == (int)HttpStatusCode.OK && !response.Bodyless
            ? await File.ReadAllBytesAsync(BodyPath(directory, url), cancellationToken)
                .ConfigureAwait(false)
            : [];
        var content = new ByteArrayContent(body);
        if (response.ContentType is { } contentType)
        {
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        return content;
    }

    // The recorder's layout (write-fixtures.mjs): host, then the URL path.
    internal static string BodyPath(string directory, Uri url) =>
        Path.Combine(
            [directory, url.Host, .. url.AbsolutePath.Split(UrlSeparator, SplitOptions)]);

    private sealed record Fault(Func<Uri, bool> Matches, HttpStatusCode Status);
}
