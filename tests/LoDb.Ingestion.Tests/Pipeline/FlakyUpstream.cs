using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// The recorded upstream, with failures that end: the next requests matching a rule fail,
/// then the recording answers again; and bodies served instead of the recording.
/// </summary>
/// <remarks>
/// <see cref="FixtureReplayHandler.FailWith"/> fails for good; a recovery on the same
/// instance, where caches live, needs failures that stop.
/// </remarks>
internal sealed class FlakyUpstream : DelegatingHandler
{
    private readonly Lock gate = new();
    private readonly List<Failure> failures = [];
    private readonly List<Served> bodies = [];
    private readonly ConcurrentQueue<Uri> served = new();

    public FlakyUpstream(FixtureReplayHandler replay)
        : base(replay)
    {
        Replay = replay;
    }

    public FixtureReplayHandler Replay { get; }

    /// <summary>The requests answered by <see cref="Serve"/>, in arrival order.</summary>
    public IReadOnlyList<Uri> ServedRequests => [.. served];

    /// <summary>
    /// Answers 200 with <paramref name="body"/> to every request matching
    /// <paramref name="matches"/> that no failure takes: an image the recording lacks.
    /// </summary>
    public void Serve(Func<Uri, bool> matches, byte[] body, string mediaType)
    {
        lock (gate)
        {
            bodies.Add(new Served(matches, body, mediaType));
        }
    }

    /// <summary>
    /// Answers <paramref name="status"/> to the next <paramref name="count"/> requests
    /// matching <paramref name="matches"/>, retries included.
    /// </summary>
    public void FailNext(Func<Uri, bool> matches, int count, HttpStatusCode status)
    {
        lock (gate)
        {
            failures.Add(new Failure(matches, status) { Remaining = count });
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var url = request.RequestUri!;
        if (Take(url) is { } status)
        {
            return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
        }

        return BodyFor(url) is { } body
            ? Task.FromResult(Answer(request, url, body))
            : base.SendAsync(request, cancellationToken);
    }

    private Served? BodyFor(Uri url)
    {
        lock (gate)
        {
            return bodies.Find(rule => rule.Matches(url));
        }
    }

    private HttpResponseMessage Answer(HttpRequestMessage request, Uri url, Served body)
    {
        served.Enqueue(url);
        var content = new ByteArrayContent(body.Body);
        content.Headers.ContentType = new MediaTypeHeaderValue(body.MediaType);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = content,
        };
    }

    private HttpStatusCode? Take(Uri url)
    {
        lock (gate)
        {
            var failure = failures.Find(rule => rule.Remaining > 0 && rule.Matches(url));
            if (failure is null)
            {
                return null;
            }

            failure.Remaining--;
            return failure.Status;
        }
    }

    private sealed record Served(Func<Uri, bool> Matches, byte[] Body, string MediaType);

    private sealed record Failure(Func<Uri, bool> Matches, HttpStatusCode Status)
    {
        public int Remaining { get; set; }
    }
}
