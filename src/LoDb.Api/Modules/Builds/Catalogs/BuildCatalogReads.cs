using System.Diagnostics.CodeAnalysis;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;

namespace LoDb.Api.Modules.Builds.Catalogs;

/// <summary>
/// Opens the patch a list of builds renders on: the version the visitor browses, else the
/// latest.
/// </summary>
internal sealed class BuildCatalogReads(CatalogGateway gateway, BuildVersions versions)
{
    /// <summary>
    /// Whether <paramref name="read"/> failed on the caller's query, a malformed or unknown
    /// version or language, rather than on a catalog not readable now, which a page shows
    /// with ghosts instead.
    /// </summary>
    public static bool IsClientError(
        CatalogRead read,
        [NotNullWhen(true)] out CatalogProblem? problem)
    {
        ArgumentNullException.ThrowIfNull(read);
        problem = read.IsOpen || read.Problem.Status >= StatusCodes.Status500InternalServerError
            ? null
            : read.Problem;
        return problem is not null;
    }

    /// <param name="scope">The version browsed, the latest when null, and the language.</param>
    /// <param name="cancellationToken">Aborts the wait, never a shared ingestion.</param>
    public async Task<CatalogRead> OpenAsync(
        CatalogScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var version = scope.Version ?? (await versions.LatestAsync(cancellationToken))?.Value;
        return version is null
            ? CatalogRead.Fail(CatalogProblem.UpstreamUnavailable())
            : await gateway.ReadAsync(scope with { Version = version }, cancellationToken);
    }
}
