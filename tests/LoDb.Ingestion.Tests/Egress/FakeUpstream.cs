using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace LoDb.Ingestion.Tests.Egress;

/// <summary>
/// Primary handler standing in for the CDN: answers from a script, records every request,
/// never touches the network.
/// </summary>
internal sealed class FakeUpstream(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    private readonly ConcurrentQueue<HttpRequestMessage> requests = new();

    public IReadOnlyList<HttpRequestMessage> Requests => [.. requests];

    public int Calls => requests.Count;

    public static FakeUpstream Answering(HttpStatusCode status, string body = "") =>
        new((_, _) => Task.FromResult(Response(status, body)));

    public static FakeUpstream Answering(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new((request, _) => Task.FromResult(respond(request)));

    /// <summary>
    /// Answers each call with the next response of the list, the last one repeated.
    /// </summary>
    public static FakeUpstream Sequence(params HttpStatusCode[] statuses)
    {
        var next = 0;
        return new((_, _) =>
        {
            var index = Math.Min(Interlocked.Increment(ref next) - 1, statuses.Length - 1);
            return Task.FromResult(Response(statuses[index], "body"));
        });
    }

    public static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) },
        };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        requests.Enqueue(request);
        return respond(request, cancellationToken);
    }
}
