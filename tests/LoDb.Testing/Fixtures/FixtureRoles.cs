namespace LoDb.Testing.Fixtures;

/// <summary>
/// The two complete versions of the recording, the newest ones on the day it was made: every
/// dataset and every ingested image of theirs is recorded.
/// </summary>
public sealed record FixtureRoles
{
    public required string Latest { get; init; }

    public required string Previous { get; init; }
}
