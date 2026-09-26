namespace LoDb.Testing.Fixtures;

/// <summary>
/// The table of contents of the recording, written by <c>tools/next/fixtures/record.mjs</c>.
/// </summary>
public sealed record FixtureIndex
{
    /// <summary>Day of the recording (yyyy-MM-dd).</summary>
    public required string RecordedOn { get; init; }

    public required FixtureRoles Roles { get; init; }

    /// <summary>Every recorded answer, sorted by URL.</summary>
    public required IReadOnlyList<RecordedResponse> Responses { get; init; }
}
