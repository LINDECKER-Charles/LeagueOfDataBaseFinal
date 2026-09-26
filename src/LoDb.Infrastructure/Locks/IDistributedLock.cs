namespace LoDb.Infrastructure.Locks;

/// <summary>
/// A lock shared by every instance of the API, taken without waiting.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Takes the lock named <paramref name="name"/> if no one holds it.
    /// </summary>
    /// <returns>
    /// A handle that releases the lock when disposed, or null if another holder has it.
    /// </returns>
    Task<IAsyncDisposable?> TryAcquireAsync(string name, CancellationToken cancellationToken);
}
