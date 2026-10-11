namespace LoDb.Api.Modules.Accounts.Security.Limits;

/// <summary>
/// The bound of one <see cref="AccountActionLimit"/>, named after its policy and read from
/// <c>LoDb:RateLimiting:&lt;Setting&gt;</c>, checked when the host starts.
/// </summary>
internal sealed class AccountActionLimitOptions
{
    /// <summary>Above this, the bound limits nothing a client could send.</summary>
    public const int MostPerHour = 1_000_000;

    /// <summary>Actions of one account per sliding hour.</summary>
    public int PerHour { get; set; }

    /// <summary>Whether the bound lets at least one action through and stays sane.</summary>
    public bool IsUsable() => PerHour is >= 1 and <= MostPerHour;
}
