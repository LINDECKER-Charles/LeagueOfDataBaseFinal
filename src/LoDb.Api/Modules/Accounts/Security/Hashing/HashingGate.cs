namespace LoDb.Api.Modules.Accounts.Security.Hashing;

/// <summary>Bounds the hash computations running at once, and so their memory.</summary>
/// <remarks>
/// <para>
/// A computation holds its whole memory cost until it ends: 19 MiB at the target, 64 MiB
/// for a legacy hash at PHP's defaults. Two at once keep the peak far below the API's 1152m
/// limit, whatever the number of sign-ins; the others wait a few tens of milliseconds.
/// </para>
/// <para>
/// A safety net: the requests that hash first take a permit of the asynchronous admission,
/// which lets fewer of them through than this gate has slots, so no request thread blocks
/// here. The command host and Identity's own paths still come through it.
/// </para>
/// </remarks>
internal sealed class HashingGate(SecurityMetrics metrics) : IDisposable
{
    /// <summary>Computations allowed at once.</summary>
    public const int MaxConcurrentComputations = 2;

    private readonly SemaphoreSlim _slots =
        new(MaxConcurrentComputations, MaxConcurrentComputations);

    // Synchronous, as IPasswordHasher is: the wait only lasts while two computations run.
    public T Run<T>(Func<T> computation)
    {
        ArgumentNullException.ThrowIfNull(computation);
        _slots.Wait();
        metrics.ComputationStarted();
        try
        {
            return computation();
        }
        finally
        {
            metrics.ComputationEnded();
            _slots.Release();
        }
    }

    public void Dispose() => _slots.Dispose();
}
