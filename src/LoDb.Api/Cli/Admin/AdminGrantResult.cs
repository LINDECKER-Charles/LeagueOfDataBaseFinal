namespace LoDb.Api.Cli.Admin;

/// <summary>The administrator account <c>admin create</c> left.</summary>
internal sealed record AdminGrantResult
{
    public required string Username { get; init; }

    public required string Email { get; init; }

    /// <summary>The password of an account created by the command; null for one found.</summary>
    public string? Password { get; init; }

    /// <summary>False when the account already was an administrator.</summary>
    public required bool Promoted { get; init; }

    public required bool TwoFactorEnabled { get; init; }
}
