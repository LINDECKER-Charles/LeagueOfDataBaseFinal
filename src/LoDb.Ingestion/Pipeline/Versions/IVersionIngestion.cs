using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>
/// Ingests a version: the datasets of its languages, then the images of the four resources
/// (ADR 0003).
/// </summary>
/// <remarks>
/// A run holds the version's lock, so two instances never ingest the same version at once. It
/// is idempotent and resumable: what is stored is skipped, and a failure during the run
/// leaves the version to a later attempt instead of throwing. Only a failure to take the lock
/// or to read Data Dragon's lists throws.
/// </remarks>
public interface IVersionIngestion
{
    Task<VersionIngestionResult> IngestAsync(
        PatchVersion version,
        IngestionRequest request,
        CancellationToken cancellationToken);
}
