using System.Security.Claims;
using LoDb.Api.Modules.Accounts.Registration;

namespace LoDb.Api.Modules.Accounts.Google;

/// <summary>What a Google sign-in tells about its account, read from the userinfo claims.</summary>
internal sealed record GoogleProfile
{
    /// <summary>
    /// Claim of a Google-verified e-mail, mapped from <c>email_verified</c>; absent otherwise.
    /// </summary>
    public const string EmailVerifiedClaim = "email_verified";

    /// <summary>Value of <see cref="EmailVerifiedClaim"/>.</summary>
    public const string Verified = "true";

    /// <summary>The stable id of the Google account, the <c>sub</c> claim.</summary>
    public required string Subject { get; init; }

    /// <summary>The e-mail, lowercase as the accounts store it.</summary>
    public required string Email { get; init; }

    /// <summary>Whether Google vouches that the e-mail belongs to the account.</summary>
    public required bool EmailVerified { get; init; }

    /// <summary>The first hint of a username for a new account.</summary>
    public string? GivenName { get; init; }

    /// <summary>The profile the claims describe; null without a subject or an e-mail.</summary>
    public static GoogleProfile? From(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new GoogleProfile
        {
            Subject = subject,
            Email = RegistrationRules.Email(email),
            EmailVerified = principal.HasClaim(EmailVerifiedClaim, Verified),
            GivenName = principal.FindFirstValue(ClaimTypes.GivenName),
        };
    }
}
