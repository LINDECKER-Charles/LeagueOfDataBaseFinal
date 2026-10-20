using System.Net.Http.Headers;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>How long the pass waits before it tries a page again.</summary>
internal static class RetryDelays
{
    /// <summary>The SSR's own Retry-After on a refusal, when an answer names none.</summary>
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(30);

    // A refusal is waited at least a second, so that a Retry-After of zero never spins, and
    // five minutes at most, so that a pause or a cancel is seen within that.
    private static readonly TimeSpan Shortest = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Longest = TimeSpan.FromMinutes(5);

    // A page that failed is tried again a little later, then later still: a restarting SSR or
    // a throttling store has settled by then, a broken page has not.
    private static readonly TimeSpan[] AfterFailures =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)];

    /// <summary>The tries a page gets before it is counted failed.</summary>
    public static int MaxAttempts => AfterFailures.Length + 1;

    /// <summary>The wait an answer's <c>Retry-After</c> asks for, if it asks.</summary>
    public static TimeSpan? Of(RetryConditionHeaderValue? header, DateTimeOffset now) =>
        header switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - now,
            _ => null,
        };

    /// <summary>The wait after a refusal: what it asked, within bounds.</summary>
    public static TimeSpan AfterRefusal(TimeSpan? asked)
    {
        var wait = asked ?? Default;
        return wait < Shortest ? Shortest : wait > Longest ? Longest : wait;
    }

    /// <summary>The wait after the <paramref name="failures"/>-th failed try of a page.</summary>
    public static TimeSpan AfterFailure(int failures) =>
        AfterFailures[Math.Clamp(failures - 1, 0, AfterFailures.Length - 1)];
}
