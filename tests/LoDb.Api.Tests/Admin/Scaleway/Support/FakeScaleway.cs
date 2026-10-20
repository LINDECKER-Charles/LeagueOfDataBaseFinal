using System.Collections.Concurrent;

namespace LoDb.Api.Tests.Admin.Scaleway.Support;

/// <summary>
/// Scaleway's billing API as the <c>scaleway-billing</c> client reaches it: it records each
/// request and answers with <see cref="Answer"/>, once <see cref="Gate"/> opens if one is set
/// and <see cref="IsHeld"/> holds the request.
/// </summary>
internal sealed class FakeScaleway : HttpMessageHandler
{
    private const string TokenHeader = "X-Auth-Token";

    private readonly ConcurrentQueue<ScalewayCall> _calls = new();

    public Func<ScalewayCall, HttpResponseMessage> Answer { get; set; } = ScalewayAnswers.Default;

    /// <summary>Holds the answers <see cref="IsHeld"/> picks until it completes.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>The requests <see cref="Gate"/> holds: every one unless a test says.</summary>
    public Func<ScalewayCall, bool> IsHeld { get; set; } = static _ => true;

    public IReadOnlyCollection<ScalewayCall> Calls => _calls;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = request.Headers.TryGetValues(TokenHeader, out var values)
            ? string.Join(",", values)
            : null;
        var call = new ScalewayCall(request.RequestUri!, token);
        _calls.Enqueue(call);
        if (Gate is { } gate && IsHeld(call))
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        return Answer(call);
    }
}
