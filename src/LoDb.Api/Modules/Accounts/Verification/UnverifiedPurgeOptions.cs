using System.ComponentModel.DataAnnotations;

namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>
/// When an account that never verified its e-mail is erased
/// (<c>LoDb:Accounts:UnverifiedPurge</c>), checked when the host starts.
/// </summary>
internal sealed class UnverifiedPurgeOptions
{
    public const string SectionName = "LoDb:Accounts:UnverifiedPurge";

    private const int DefaultAfterDays = 7;
    private const int DefaultBatchSize = 500;
    private const int MostPerRun = 10_000;

    // A day at least: the owner of a new account must have the time to open the link.
    private static readonly TimeSpan ShortestDelay = TimeSpan.FromDays(1);
    private static readonly TimeSpan LongestDelay = TimeSpan.FromDays(365);

    /// <summary>The age past which an account still unverified is erased.</summary>
    public TimeSpan After { get; set; } = TimeSpan.FromDays(DefaultAfterDays);

    /// <summary>Accounts erased per run at most; the next run takes the rest.</summary>
    [Range(1, MostPerRun)]
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>Whether the delay leaves the owner time, which a range cannot state.</summary>
    public bool HasUsableDelay() => After >= ShortestDelay && After <= LongestDelay;
}
