using System.Diagnostics.CodeAnalysis;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Api.Modules.Profiles.Reading;

/// <summary>
/// Opens the catalog a profile resolves its favorites on: the version it pins while Data
/// Dragon lists it, else the version browsed, else the latest.
/// </summary>
/// <remarks>
/// Showing and saving resolve on the same version, so a favorite absent from the browsed
/// version neither vanishes from the page nor is wiped by the next save.
/// </remarks>
internal sealed partial class ProfileCatalog(
    ICatalogReader reader,
    CatalogGateway gateway,
    ILogger<ProfileCatalog> logger)
{
    /// <summary>
    /// Whether <paramref name="read"/> failed on the caller's query, a malformed or unknown
    /// version or language, rather than on a catalog not readable now, which a page shows
    /// without its favorites instead.
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

    /// <param name="pinned">The profile's <c>preferred_version</c>.</param>
    /// <param name="browsing">The version browsed, if any, and the language of the names.</param>
    /// <param name="cancellationToken">Aborts the wait, never a shared ingestion.</param>
    public async Task<CatalogRead> OpenAsync(
        string? pinned,
        CatalogScope browsing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(browsing);
        var version = await EffectiveVersionAsync(pinned, browsing.Version, cancellationToken);
        return version is null
            ? CatalogRead.Fail(CatalogProblem.UpstreamUnavailable())
            : await gateway.ReadAsync(browsing with { Version = version }, cancellationToken);
    }

    /// <summary>
    /// Whether Data Dragon lists <paramref name="version"/>; null when its list could not be
    /// read.
    /// </summary>
    public async Task<bool?> IsListedAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            var versions = await reader.GetVersionsAsync(cancellationToken);
            return versions.Listed.Contains(version);
        }
        catch (EgressException exception)
        {
            LogVersionsFailed(logger, exception);
            return null;
        }
    }

    // Without Data Dragon's list, a pin is trusted: it was listed when it was saved.
    private async Task<string?> EffectiveVersionAsync(
        string? pinned,
        string? browsing,
        CancellationToken cancellationToken)
    {
        CatalogVersions versions;
        try
        {
            versions = await reader.GetVersionsAsync(cancellationToken);
        }
        catch (EgressException exception)
        {
            LogVersionsFailed(logger, exception);
            return pinned ?? browsing;
        }

        if (PatchVersion.TryParse(pinned, out var pin) && versions.Listed.Contains(pin))
        {
            return pin.Value;
        }

        var newest = versions.Listed.Count > 0 ? versions.Listed[0] : null;
        return browsing ?? (versions.Latest ?? newest)?.Value;
    }

    [LoggerMessage(
        EventName = "profile.versions.failed",
        Level = LogLevel.Warning,
        Message = "Profile version unsettled: Data Dragon's version list could not be read.")]
    private static partial void LogVersionsFailed(ILogger logger, Exception exception);
}
