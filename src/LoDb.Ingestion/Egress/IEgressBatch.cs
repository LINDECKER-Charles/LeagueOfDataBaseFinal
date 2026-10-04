namespace LoDb.Ingestion.Egress;

/// <summary>
/// A group of fetches reported together. Thread-safe: its fetches may run in parallel.
/// </summary>
/// <remarks>
/// Disposing it emits at most one <c>fetch.allowlist.refused</c>, one
/// <c>fetch.redirect.refused</c> and one <c>fetch.batch.degraded</c> line.
/// </remarks>
public interface IEgressBatch : IDisposable
{
    /// <inheritdoc cref="IEgressFetcher.FetchAsync"/>
    Task<FetchOutcome> FetchAsync(Uri url, CancellationToken cancellationToken);
}
