using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Api.Modules.Builds.Catalogs;

/// <summary>
/// What Data Dragon lists: the patch a build may pin, the language it may be written in.
/// Each answer is null when Data Dragon could not be read, which the caller turns into a
/// 503 rather than into a verdict on the build.
/// </summary>
internal sealed partial class BuildVersions(ICatalogReader reader, ILogger<BuildVersions> logger)
{
    /// <summary>The newest patch; null when Data Dragon could not be read or lists none.</summary>
    public async Task<PatchVersion?> LatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            var versions = await reader.GetVersionsAsync(cancellationToken);
            return versions.Latest ?? (versions.Listed.Count > 0 ? versions.Listed[0] : null);
        }
        catch (EgressException exception)
        {
            LogListFailed(logger, exception);
            return null;
        }
    }

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
            LogListFailed(logger, exception);
            return null;
        }
    }

    public async Task<bool?> IsKnownAsync(
        DdragonLanguage language,
        CancellationToken cancellationToken)
    {
        try
        {
            var languages = await reader.GetLanguagesAsync(cancellationToken);
            return languages.Contains(language);
        }
        catch (EgressException exception)
        {
            LogListFailed(logger, exception);
            return null;
        }
    }

    [LoggerMessage(
        EventName = "build.versions.failed",
        Level = LogLevel.Warning,
        Message = "Build patch unsettled: Data Dragon's lists could not be read.")]
    private static partial void LogListFailed(ILogger logger, Exception exception);
}
