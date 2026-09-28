using System.Net;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// The recorded upstream, with failures that end: the next requests matching a rule fail,
/// then the recording answers again.
/// </summary>
/// <remarks>
/// <see cref="FixtureReplayHandler.FailWith"/> fails for good; a recovery on the same
/// instance, where caches live, needs failures that stop.
/// </remarks>
internal sealed class FlakyUpstream : DelegatingHandler
{
    private readonly Lock gate = new();
    private readonly List<Failure> failures = [];

    public FlakyUpstream(FixtureReplayHandler replay)
        : base(replay)
    {
        Replay = replay;
    }

    public FixtureReplayHandler Replay { get; }

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
        return Take(request.RequestUri!) is { } status
            ? Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request })
            : base.SendAsync(request, cancellationToken);
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

    private sealed record Failure(Func<Uri, bool> Matches, HttpStatusCode Status)
    {
        public int Remaining { get; set; }
    }
}
