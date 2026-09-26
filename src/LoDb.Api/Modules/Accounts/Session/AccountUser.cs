namespace LoDb.Api.Modules.Accounts.Session;

/// <summary>The signed-in account, as the fronts show it.</summary>
internal sealed record AccountUser
{
    public required int Id { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    /// <summary>The Riot tag line shown after the username, such as <c>EUW</c>.</summary>
    public required string? RiotTagline { get; init; }

    /// <summary>Whether the e-mail is verified, which some actions require.</summary>
    public required bool EmailVerified { get; init; }

    /// <summary>False for an account created through Google, which signs in there.</summary>
    public required bool HasPassword { get; init; }

    public required bool IsSupporter { get; init; }

    /// <summary>Whether a sign-in with the password also asks for an authenticator code.</summary>
    public required bool TwoFactorEnabled { get; init; }

    /// <summary>
    /// Whether this session was opened with a second factor, which the admin area requires.
    /// </summary>
    public required bool MultiFactor { get; init; }

    /// <summary>Roles of the account, such as <c>Admin</c>.</summary>
    public required IReadOnlyList<string> Roles { get; init; }
}
