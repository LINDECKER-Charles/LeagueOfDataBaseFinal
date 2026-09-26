using System.Text.RegularExpressions;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Api.Modules.Profiles.Http;

namespace LoDb.Api.Modules.Profiles.Identity;

/// <summary>
/// The rules of a summoner identity: the username of a new account, and a Riot tag line of 3
/// to 5 letters or digits, as Riot writes it.
/// </summary>
/// <remarks>Inputs are trimmed before any rule applies; a blank tag line means none.</remarks>
internal static partial class IdentityRules
{
    public const string TaglineInvalid = "tagline-invalid";

    private const string TaglinePattern = "^[A-Za-z0-9]{3,5}\\z";

    /// <summary>The tag line as stored: trimmed, null when blank.</summary>
    public static string? Tagline(string? tagline)
    {
        var trimmed = tagline?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Every rule the stored-form identity breaks, a taken username aside.</summary>
    public static FieldErrors Check(string username, string? tagline)
    {
        ArgumentNullException.ThrowIfNull(username);
        var errors = new FieldErrors();
        errors.RequireText(ProfileFields.Username, username);
        if (username.Length > 0 && !RegistrationRules.IsUsername(username))
        {
            errors.Add(ProfileFields.Username, RegistrationRules.UsernameInvalid);
        }

        if (tagline is not null && !TaglineSyntax().IsMatch(tagline))
        {
            errors.Add(ProfileFields.RiotTagline, TaglineInvalid);
        }

        return errors;
    }

    [GeneratedRegex(TaglinePattern, RegexOptions.CultureInvariant)]
    private static partial Regex TaglineSyntax();
}
