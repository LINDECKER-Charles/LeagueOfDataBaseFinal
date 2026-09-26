namespace LoDb.Desktop.Auth.Google;

/// <summary>The stage of the last Google sign-in, and its failure code when it failed.</summary>
internal sealed record GoogleStatus
{
    public static readonly GoogleStatus Idle = new() { Stage = GoogleStage.Idle };

    public static readonly GoogleStatus Pending = new() { Stage = GoogleStage.Pending };

    public required GoogleStage Stage { get; init; }

    /// <summary>One of <see cref="GoogleFailures"/>, or the API's own problem code.</summary>
    public string? Failure { get; init; }

    public static GoogleStatus FailedWith(string failure) =>
        new() { Stage = GoogleStage.Failed, Failure = failure };
}
