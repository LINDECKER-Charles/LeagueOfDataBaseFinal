using LoDb.Api.Modules.Builds.Storage;
using LoDb.Domain.Builds.Import;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Import;

/// <summary>A build carried over to another patch, and what the carrying changed.</summary>
internal sealed record ImportPreview
{
    public required BuildDraft Draft { get; init; }

    public required ImportReport Report { get; init; }

    /// <param name="source">The build imported, which stays as it is.</param>
    /// <param name="version">The patch it was projected on.</param>
    /// <param name="projection">Its structure on that patch, and the report.</param>
    public static ImportPreview Of(Build source, string version, BuildProjection projection)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(projection);
        return new ImportPreview
        {
            Draft = new BuildDraft
            {
                Name = source.Name,
                Description = source.Description,
                IsPublic = false,
                GameVersion = version,
                GameMode = StoredMode.Of(source),
                Language = source.Language,
                Structure = projection.Structure,
            },
            Report = projection.Report,
        };
    }
}
