namespace LoDb.Desktop.Lifecycle;

/// <summary>The update state at one moment.</summary>
internal sealed record UpdateSnapshot
{
    public static readonly UpdateSnapshot None = new() { Stage = UpdateStage.None };

    public required UpdateStage Stage { get; init; }

    /// <summary>The version being downloaded or ready; null with no update.</summary>
    public string? Version { get; init; }
}
