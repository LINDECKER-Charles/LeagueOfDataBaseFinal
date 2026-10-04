using System.Security.Cryptography;
using System.Text;

namespace LoDb.Desktop.Auth.Google;

/// <summary>
/// The Google sign-in in progress: one at a time, a new one replacing the previous, each
/// state accepted once and for a limited time.
/// </summary>
internal sealed class GoogleFlows(TimeProvider time)
{
    /// <summary>Time left to the user on Google's page before the flow expires.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private GoogleFlow? _pending;
    private GoogleStatus _last = GoogleStatus.Idle;

    public GoogleStatus Status
    {
        get
        {
            lock (_lock)
            {
                return _pending switch
                {
                    null => _last,
                    { } flow when IsExpired(flow) =>
                        GoogleStatus.FailedWith(GoogleFailures.Expired),
                    _ => GoogleStatus.Pending,
                };
            }
        }
    }

    public GoogleFlow Begin(Uri redirectUri, bool isRemembered)
    {
        var flow = new GoogleFlow
        {
            State = GoogleAuthorization.NewSecret(),
            Verifier = GoogleAuthorization.NewSecret(),
            RedirectUri = redirectUri.AbsoluteUri,
            IsRemembered = isRemembered,
            ExpiresAt = time.GetUtcNow() + Lifetime,
        };
        lock (_lock)
        {
            _pending = flow;
        }

        return flow;
    }

    /// <summary>
    /// Takes the running flow if the state is its own and still valid; null otherwise, and
    /// the running flow is left alone (a forged redirect must not end it).
    /// </summary>
    public GoogleFlow? Take(string? state)
    {
        lock (_lock)
        {
            if (_pending is not { } flow
                || state is null
                || !HaveSameBytes(flow.State, state)
                || IsExpired(flow))
            {
                return null;
            }

            _pending = null;
            _last = GoogleStatus.Pending;
            return flow;
        }
    }

    /// <summary>Records how a taken flow ended, unless a newer one has begun since.</summary>
    public void End(GoogleStatus status)
    {
        lock (_lock)
        {
            if (_pending is null)
            {
                _last = status;
            }
        }
    }

    /// <summary>Ends a flow that never reached Google (the browser did not open).</summary>
    public void Abandon(GoogleFlow flow, string failure)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_pending, flow))
            {
                _pending = null;
                _last = GoogleStatus.FailedWith(failure);
            }
        }
    }

    private bool IsExpired(GoogleFlow flow) => time.GetUtcNow() >= flow.ExpiresAt;

    // Constant time: the comparison must not tell how much of a guessed state was right.
    private static bool HaveSameBytes(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
}
