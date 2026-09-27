namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>The immutable datasets, <c>data/{version}/{lang}/{type}.json</c>.</summary>
internal sealed record DatasetFigures
{
    public required IReadOnlyList<StorageRow> ByVersion { get; init; }

    public required IReadOnlyList<StorageRow> ByLang { get; init; }

    public required IReadOnlyList<StorageRow> ByType { get; init; }
}
