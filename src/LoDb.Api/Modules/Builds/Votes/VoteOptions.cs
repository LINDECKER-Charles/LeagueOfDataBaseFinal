namespace LoDb.Api.Modules.Builds.Votes;

/// <summary>
/// Who may vote on the public builds (<c>LoDb:Builds:Votes</c>), checked when the host
/// starts.
/// </summary>
internal sealed class VoteOptions
{
    public const string SectionName = "LoDb:Builds:Votes";

    private const int DefaultMinimumAgeHours = 24;

    private static readonly TimeSpan LongestMinimumAge = TimeSpan.FromDays(30);

    /// <summary>
    /// How long an account exists before its votes count; zero lets a new account vote at
    /// once.
    /// </summary>
    public TimeSpan MinimumAccountAge { get; set; } = TimeSpan.FromHours(DefaultMinimumAgeHours);

    /// <summary>Whether the age is neither negative nor long enough to shut voting.</summary>
    public bool HasUsableMinimumAge() =>
        MinimumAccountAge >= TimeSpan.Zero && MinimumAccountAge <= LongestMinimumAge;
}
