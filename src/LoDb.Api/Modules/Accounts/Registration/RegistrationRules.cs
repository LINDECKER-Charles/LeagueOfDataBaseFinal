using System.Text.RegularExpressions;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Security.Policy;

namespace LoDb.Api.Modules.Accounts.Registration;

/// <summary>
/// The rules of a new account, those of the legacy form: an HTML5 e-mail with a top-level
/// domain, a username of the legacy pattern, the CNIL password policy, accepted terms.
/// </summary>
/// <remarks>Inputs are trimmed, and the e-mail lowercased, before any rule applies.</remarks>
internal static partial class RegistrationRules
{
    /// <summary>Length of <c>users.email</c>.</summary>
    public const int MaxEmailLength = 180;

    public const string EmailInvalid = "email-invalid";
    public const string EmailTooLong = "email-too-long";
    public const string EmailTaken = "email-taken";
    public const string UsernameInvalid = "username-invalid";
    public const string UsernameTaken = "username-taken";
    public const string TermsRequired = "terms-required";

    private const string LocalPart = "[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+";
    private const string DomainLabel = "[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?";

    // Symfony's html5 mode: the domain needs a dot, so a top-level domain.
    private const string EmailPattern =
        "^" + LocalPart + "@" + DomainLabel + "(?:\\." + DomainLabel + ")+\\z";

    private const string UsernamePattern = "^[a-zA-Z0-9][a-zA-Z0-9_.-]{2,23}\\z";

    /// <summary>The e-mail as stored: trimmed and lowercase.</summary>
    public static string Email(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>The username as stored: trimmed, its case kept.</summary>
    public static string Username(string? username) => (username ?? string.Empty).Trim();

    public static bool IsUsername(string username) => UsernameSyntax().IsMatch(username);

    /// <summary>Every rule <paramref name="request"/> breaks, taken names aside.</summary>
    public static FieldErrors Check(RegisterRequest request)
    {
        var errors = new FieldErrors();
        CheckEmail(errors, Email(request.Email));
        var username = Username(request.Username);
        errors.RequireText(AccountFields.Username, username);
        if (username.Length > 0 && !IsUsername(username))
        {
            errors.Add(AccountFields.Username, UsernameInvalid);
        }

        CheckPassword(errors, request.Password);
        if (!request.AcceptTerms)
        {
            errors.Add(AccountFields.AcceptTerms, TermsRequired);
        }

        return errors;
    }

    /// <summary>Adds the rule a stored-form e-mail breaks, if any.</summary>
    public static void CheckEmail(FieldErrors errors, string email)
    {
        if (email.Length == 0)
        {
            errors.Add(AccountFields.Email, FieldErrors.Required);
        }
        else if (email.Length > MaxEmailLength)
        {
            errors.Add(AccountFields.Email, EmailTooLong);
        }
        else if (!EmailSyntax().IsMatch(email))
        {
            errors.Add(AccountFields.Email, EmailInvalid);
        }
    }

    /// <summary>Adds the CNIL rules a new password breaks, all at once.</summary>
    public static void CheckPassword(FieldErrors errors, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            errors.Add(AccountFields.Password, FieldErrors.Required);
            return;
        }

        errors.Add(AccountFields.Password, CnilPasswordValidator.Validate(password).Errors);
    }

    [GeneratedRegex(EmailPattern, RegexOptions.CultureInvariant)]
    private static partial Regex EmailSyntax();

    [GeneratedRegex(UsernamePattern, RegexOptions.CultureInvariant)]
    private static partial Regex UsernameSyntax();
}
