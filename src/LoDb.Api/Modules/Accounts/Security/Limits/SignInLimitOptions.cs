using System.ComponentModel.DataAnnotations;

namespace LoDb.Api.Modules.Accounts.Security.Limits;

/// <summary>
/// Bounds of the sign-ins and password confirmations (<c>LoDb:RateLimiting</c>), checked
/// when the host starts.
/// </summary>
/// <remarks>
/// The failures counted per identifier stay under the lockout threshold of the accounts, so
/// that one address alone runs out of attempts before it locks the account it names.
/// </remarks>
internal sealed class SignInLimitOptions
{
    public const string SectionName = "LoDb:RateLimiting";

    private const int DefaultSignInsPerWindow = 10;
    private const int DefaultFailuresPerIdentifier = 4;
    private const int DefaultPasswordConfirmsPerWindow = 5;
    private const int DefaultWindowMinutes = 15;
    private const int MostPerWindow = 1_000_000;

    // The window slides in fifteen segments, each of which must last.
    private static readonly TimeSpan ShortestWindow = TimeSpan.FromSeconds(15);

    /// <summary>Sign-ins from one address (or IPv6 /64) per window, successful or not.</summary>
    [Range(1, MostPerWindow)]
    public int SignInPerWindow { get; set; } = DefaultSignInsPerWindow;

    /// <summary>
    /// Failed sign-ins per window for one identifier from one address, below the lockout.
    /// </summary>
    [Range(1, AccountsSecurityRegistration.MaxFailedAccessAttempts - 1)]
    public int SignInFailuresPerIdentifier { get; set; } = DefaultFailuresPerIdentifier;

    /// <summary>Password confirmations per window for one signed-in account.</summary>
    [Range(1, MostPerWindow)]
    public int PasswordConfirmsPerWindow { get; set; } = DefaultPasswordConfirmsPerWindow;

    /// <summary>The sliding window of the three bounds above.</summary>
    public TimeSpan AccountWindow { get; set; } = TimeSpan.FromMinutes(DefaultWindowMinutes);

    /// <summary>Whether the window is long enough, which a range cannot state.</summary>
    public bool HasUsableWindow() => AccountWindow >= ShortestWindow;
}
