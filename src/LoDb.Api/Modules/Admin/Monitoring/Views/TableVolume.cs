namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>A table and its weight on disk, indexes and TOAST included.</summary>
internal sealed record TableVolume
{
    public required string Name { get; init; }

    public required long Bytes { get; init; }
}
