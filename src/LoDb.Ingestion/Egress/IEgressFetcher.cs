namespace LoDb.Ingestion.Egress;

/// <summary>
/// Guarded GET requests to Data Dragon and CommunityDragon through the <c>ddragon</c> client.
/// </summary>
public interface IEgressFetcher
{
    /// <summary>
    /// Fetches one URL as a batch of one.
    /// </summary>
    /// <exception cref="Errors.EgressException">
    /// Any failure that is not a definitive absence: transient, refused or too large.
    /// </exception>
    Task<FetchOutcome> FetchAsync(Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a batch whose refusals and failures are reported in one line each when it is
    /// disposed, never one line per URL.
    /// </summary>
    IEgressBatch OpenBatch();
}
