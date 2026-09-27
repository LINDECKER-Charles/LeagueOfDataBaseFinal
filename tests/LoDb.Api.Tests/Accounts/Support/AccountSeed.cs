namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>An account created before a test: verified and with a password by default.</summary>
public sealed record AccountSeed
{
    public const string Name = "Legende_42";
    public const string Address = "legende@example.test";

    /// <summary>A password the CNIL policy accepts.</summary>
    public const string StrongPassword = "Str0ng-passphrase!";

    public string Username { get; init; } = Name;

    public string Email { get; init; } = Address;

    /// <summary>Null for an account created through Google, which has none.</summary>
    public string? Password { get; init; } = StrongPassword;

    public bool EmailConfirmed { get; init; } = true;

    public bool Banned { get; init; }

    public string? GoogleId { get; init; }

    public string? RiotTagline { get; init; }
}
