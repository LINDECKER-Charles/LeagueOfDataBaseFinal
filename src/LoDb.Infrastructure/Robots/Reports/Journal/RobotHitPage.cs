namespace LoDb.Infrastructure.Robots.Reports.Journal;

/// <summary>A page of the journal, newest first.</summary>
public sealed record RobotHitPage
{
    public required IReadOnlyList<RobotHitView> Items { get; init; }

    public required int Page { get; init; }

    /// <summary>Whether an older page follows.</summary>
    public required bool HasMore { get; init; }
}
