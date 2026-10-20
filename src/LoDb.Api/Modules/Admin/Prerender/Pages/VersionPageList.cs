using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The pages of one version, as paths below <c>/{locale}/{version}/</c>, or why there are none
/// yet: its catalog is not stored (<see cref="CatalogLoadStatus.Pending"/>), or Data Dragon does
/// not list it (<see cref="CatalogLoadStatus.Unknown"/>).
/// </summary>
internal sealed record VersionPageList(CatalogLoadStatus Status, IReadOnlyList<string> Paths)
{
    public static VersionPageList Ready(IReadOnlyList<string> paths) =>
        new(CatalogLoadStatus.Ready, paths);

    public static VersionPageList Without(CatalogLoadStatus status) => new(status, []);
}
