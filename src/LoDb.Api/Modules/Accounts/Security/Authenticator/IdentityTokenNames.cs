namespace LoDb.Api.Modules.Accounts.Security.Authenticator;

/// <summary>
/// Where the secrets of the second factor sit in <c>identity_user_tokens</c>: Identity's own
/// rows, whose names its stores keep private, and the step of the last code accepted.
/// </summary>
/// <remarks>Stored names: they never change.</remarks>
internal static class IdentityTokenNames
{
    /// <summary>The login provider of Identity's internal tokens.</summary>
    public const string IdentityProvider = "[AspNetUserStore]";

    /// <summary>The authenticator key, sealed (<see cref="AuthenticatorKeyProtection"/>).</summary>
    public const string AuthenticatorKey = "AuthenticatorKey";

    /// <summary>The recovery codes, hashed one by one, separated by <c>;</c>.</summary>
    public const string RecoveryCodes = "RecoveryCodes";

    /// <summary>The login provider of the tokens the API adds.</summary>
    public const string LoDbProvider = "[LoDb]";

    /// <summary>The time step of the last authenticator code accepted.</summary>
    public const string AuthenticatorStep = "AuthenticatorStep";

    /// <summary>The separator of the recovery codes, as Identity writes them.</summary>
    public const char CodeSeparator = ';';
}
