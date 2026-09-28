namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>The authenticator key of an account, in base32, and its recovery codes.</summary>
public sealed record TwoFactorSecrets(string Key, IReadOnlyList<string> RecoveryCodes);
