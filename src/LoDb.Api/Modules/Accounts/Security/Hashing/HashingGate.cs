namespace LoDb.Api.Modules.Accounts.Security.Hashing;

/// <summary>Bounds the Argon2 computations running at once, and so their memory.</summary>
/// <remarks>
/// A computation holds its whole memory cost until it ends: 19 MiB at the target, 64 MiB
/// for a legacy hash at PHP's defaults. Two at once keep the peak far below the API's 384m
/// limit, whatever the number of sign-ins; the others wait a few tens of milliseconds.
/// </remarks>
internal sealed class HashingGate : IDisposable
{
    private const int MaxConcurrentComputations = 2;

    private readonly SemaphoreSlim _slots =
        new(MaxConcurrentComputations, MaxConcurrentComputations);

    // Synchronous, as IPasswordHasher is: the wait only lasts while two computations run.
    public T Run<T>(Func<T> computation)
    {
        _slots.Wait();
        try
        {
            return computation();
        }
        finally
        {
            _slots.Release();
        }
    }

    public void Dispose() => _slots.Dispose();
}
