namespace LoDb.Api.Modules.Admin.Prerender.Demand.Capture;

/// <summary>
/// Reads the SSR's refused pages (<see cref="RefusedPagesClient"/>; a fake in tests).
/// </summary>
internal interface IRefusedPagesReader
{
    /// <summary>
    /// The pages changed after <paramref name="after"/> at <paramref name="url"/>; null on any
    /// failure, which the next read tries again.
    /// </summary>
    Task<RefusedPagesReport?> ReadAsync(Uri url, long after, CancellationToken cancellationToken);
}
