namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>The objects last written on one UTC day.</summary>
internal sealed record StorageDay
{
    public required DateOnly Date { get; init; }

    public required long Objects { get; init; }

    public required long Bytes { get; init; }

    /// <summary>Bytes of this day and of the days before it.</summary>
    public required long CumulativeBytes { get; init; }
}
