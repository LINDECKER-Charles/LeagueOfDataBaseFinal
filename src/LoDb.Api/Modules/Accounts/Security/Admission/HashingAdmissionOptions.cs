using System.ComponentModel.DataAnnotations;
using LoDb.Api.Modules.Accounts.Security.Hashing;

namespace LoDb.Api.Modules.Accounts.Security.Admission;

/// <summary>
/// Bounds of the admission to password hashing (<c>LoDb:Accounts:Hashing</c>), checked when
/// the host starts.
/// </summary>
/// <remarks>
/// One permit lets about 24 target hashes through a second, one core at most; the queue
/// absorbs a burst of sign-ins, and a request that cannot get a permit within the wait is
/// refused with a 503 rather than kept on a thread.
/// </remarks>
internal sealed class HashingAdmissionOptions
{
    public const string SectionName = "LoDb:Accounts:Hashing";

    private const int DefaultPermits = 1;
    private const int DefaultQueueLimit = 8;
    private const int DefaultMaxWaitMilliseconds = 500;
    private const int DefaultRetryAfterSeconds = 5;
    private const int MostQueued = 1024;

    /// <summary>
    /// Requests hashing at once, no more than the gate's slots, so that none waits there.
    /// </summary>
    [Range(1, HashingGate.MaxConcurrentComputations)]
    public int Permits { get; set; } = DefaultPermits;

    /// <summary>Requests waiting for a permit; the next ones are refused at once.</summary>
    [Range(0, MostQueued)]
    public int QueueLimit { get; set; } = DefaultQueueLimit;

    /// <summary>The longest a request waits for a permit before it is refused.</summary>
    public TimeSpan MaxWait { get; set; } = TimeSpan.FromMilliseconds(DefaultMaxWaitMilliseconds);

    /// <summary>The <c>Retry-After</c> of a refusal.</summary>
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromSeconds(DefaultRetryAfterSeconds);

    /// <summary>Whether both delays are positive, which the ranges above cannot state.</summary>
    public bool HasPositiveDelays() => MaxWait > TimeSpan.Zero && RetryAfter > TimeSpan.Zero;
}
