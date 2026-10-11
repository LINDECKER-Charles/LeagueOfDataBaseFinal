using System.Diagnostics.Metrics;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>
/// Meter <c>LoDb.Security</c>: the password hashing (computations running, wait for a permit,
/// refusals), the sign-in attempts by result, the lockouts, the security events of the
/// administrators, and the sessions revoked by reason.
/// </summary>
/// <remarks>
/// Every tag takes its value from the closed lists below: never an identifier, an address or
/// a name, so the number of series stays fixed whatever the traffic.
/// </remarks>
internal sealed class SecurityMetrics
{
    public const string MeterName = "LoDb.Security";

    public const string ReasonTag = "reason";
    public const string ResultTag = "result";
    public const string AccountTag = "account";
    public const string ChannelTag = "channel";
    public const string IsAdminTag = "is_admin";
    public const string EventTag = "event";

    // Why a hashing was refused: no room left in the queue, or no permit within the wait.
    public const string QueueFull = "queue_full";
    public const string WaitTimeout = "timeout";

    // What became of a sign-in attempt.
    public const string Success = "success";
    public const string Failure = "failure";
    public const string TwoFactorRequired = "two_factor_required";
    public const string LockedOut = "locked_out";
    public const string Throttled = "throttled";
    public const string Banned = "banned";

    // Which account the attempt named: none found, or not looked up at all.
    public const string MemberAccount = "member";
    public const string AdminAccount = "admin";
    public const string NoAccount = "none";
    public const string UncheckedAccount = "unchecked";

    // Events of an administrator's second factor.
    public const string MfaEnrolled = "mfa_enrolled";
    public const string MfaInvitationRefused = "mfa_invitation_refused";

    // Why sessions were revoked: an app's sign-out, a sign-out everywhere, a refresh token
    // played twice.
    public const string SignedOut = "sign_out";
    public const string SignedOutEverywhere = "sign_out_everywhere";
    public const string RefreshReplayed = "refresh_replayed";

    // Prometheus' spelling of a boolean label.
    private const string True = "true";
    private const string False = "false";

    // Seconds: a wait is bounded to a fraction of a second, a computation lasts tens of ms.
    private static readonly double[] WaitBuckets =
        [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    private readonly Histogram<double> _wait;
    private readonly Counter<long> _refused;
    private readonly Counter<long> _signInAttempts;
    private readonly Counter<long> _lockouts;
    private readonly Counter<long> _adminEvents;
    private readonly Counter<long> _sessionsRevoked;
    private int _inFlight;

    public SecurityMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        meter.CreateObservableGauge(
            "lodb.hashing.in_flight",
            () => Volatile.Read(ref _inFlight),
            unit: "{computation}",
            description: "Password hash computations running now.");
        _wait = meter.CreateHistogram<double>(
            "lodb.hashing.wait",
            unit: "s",
            description: "Time a request waited for a hashing permit, granted or not.",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = WaitBuckets });
        _refused = meter.CreateCounter<long>(
            "lodb.hashing.refused",
            unit: "{request}",
            description: "Password hashings refused, by reason.");
        _signInAttempts = meter.CreateCounter<long>(
            "lodb.sign_in.attempts",
            unit: "{attempt}",
            description: "Sign-ins with a password, by result, kind of account and channel.");
        _lockouts = meter.CreateCounter<long>(
            "lodb.lockouts",
            unit: "{lockout}",
            description: "Accounts locked by failed attempts, administrators apart.");
        _adminEvents = meter.CreateCounter<long>(
            "lodb.admin.security_events",
            unit: "{event}",
            description: "Security events of the administrators' second factor.");
        _sessionsRevoked = meter.CreateCounter<long>(
            "lodb.sessions.revoked",
            unit: "{event}",
            description: "Revocations of sessions and refresh token families, by reason.");
    }

    public void ComputationStarted() => Interlocked.Increment(ref _inFlight);

    public void ComputationEnded() => Interlocked.Decrement(ref _inFlight);

    public void RecordWait(TimeSpan wait) => _wait.Record(wait.TotalSeconds);

    public void RecordHashingRefused(string reason) =>
        _refused.Add(1, new KeyValuePair<string, object?>(ReasonTag, reason));

    public void RecordSignIn(string result, string account, string channel) =>
        _signInAttempts.Add(
            1,
            new KeyValuePair<string, object?>(ResultTag, result),
            new KeyValuePair<string, object?>(AccountTag, account),
            new KeyValuePair<string, object?>(ChannelTag, channel));

    public void RecordLockout(bool isAdmin) =>
        _lockouts.Add(1, new KeyValuePair<string, object?>(IsAdminTag, isAdmin ? True : False));

    public void RecordAdminEvent(string adminEvent) =>
        _adminEvents.Add(1, new KeyValuePair<string, object?>(EventTag, adminEvent));

    public void RecordSessionsRevoked(string reason) =>
        _sessionsRevoked.Add(1, new KeyValuePair<string, object?>(ReasonTag, reason));
}
