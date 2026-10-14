using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Canonicals;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// Locates the canonical page of a detail's entity (ADR 0005, amendment of 2026-10-14), or
/// tells why it cannot yet: a catalog the decision reads is not stored (503 with
/// <c>Retry-After</c>), or Data Dragon failed (503).
/// </summary>
/// <remarks>
/// The catalog the decision reads besides the latest one, of the version listed after the
/// page's, for an entity the latest version lacks only, is admitted as the page's own: within
/// the budget of cold work, as the caller is.
/// </remarks>
internal sealed partial class CanonicalGateway(
    CanonicalReader reader,
    ILogger<CanonicalGateway> logger)
{
    /// <param name="version">The version of the page.</param>
    /// <param name="path">The entity's path in that version.</param>
    /// <param name="caller">Who asks, as the page's own read.</param>
    /// <param name="cancellationToken">The request's.</param>
    public async Task<CanonicalRead> LocateAsync(
        PatchVersion version,
        CanonicalPath path,
        ColdCaller caller,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        try
        {
            var demand = ColdDemand.Synchronous.By(caller);
            var location = await reader.LocateAsync(version, path, demand, cancellationToken);
            return location is null
                ? CanonicalRead.Fail(CatalogProblem.Pending())
                : CanonicalRead.Located(CanonicalLink.Of(location.Canonical));
        }
        catch (EgressException exception)
        {
            LogUpstreamFailed(logger, version.Value, exception);
            return CanonicalRead.Fail(CatalogProblem.UpstreamUnavailable());
        }
    }

    [LoggerMessage(
        EventName = "catalog.canonical.failed",
        Level = LogLevel.Warning,
        Message = "Canonical of a {Version} page unknown: Data Dragon failed.")]
    private static partial void LogUpstreamFailed(
        ILogger logger,
        string version,
        Exception exception);
}
