namespace LoDb.Api.Modules.Accounts.Security.Admission;

/// <summary>
/// No hashing permit came within the wait: a transient refusal, answered with a 503 and a
/// <c>Retry-After</c> by <see cref="HashingBusyFilter"/>.
/// </summary>
internal sealed class HashingBusyException(TimeSpan retryAfter)
    : Exception("Every hashing permit is taken: retry shortly.")
{
    /// <summary>When the client may try again.</summary>
    public TimeSpan RetryAfter { get; } = retryAfter;
}
