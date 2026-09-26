namespace LoDb.Desktop.Smoke;

/// <summary>One line of the smoke report: <c>{name}: {state} {detail}</c>.</summary>
internal sealed record SmokeCheck
{
    public required string Name { get; init; }

    public required string State { get; init; }

    public required string Detail { get; init; }

    /// <summary>A failed check makes <c>--smoke</c> exit with 1.</summary>
    public required bool IsHealthy { get; init; }

    public override string ToString() => $"{Name}: {State} {Detail}".TrimEnd();
}
