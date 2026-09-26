using System.Text.RegularExpressions;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Contact;

/// <summary>
/// The rules of the legacy <c>ContactSubmission</c>: a known category, an HTML5 e-mail, a message
/// of 10 to 5,000 characters, a name and a subject kept to their columns.
/// </summary>
/// <remarks>
/// Every field is trimmed first; lengths count characters, not UTF-16 units, as PHP's
/// <c>mb_strlen</c> does.
/// </remarks>
internal static partial class ContactRules
{
    public const int MaxNameLength = 120;
    public const int MaxEmailLength = 255;
    public const int MaxSubjectLength = 160;
    public const int MinMessageLength = 10;
    public const int MaxMessageLength = 5000;

    public const string Invalid = "invalid";
    public const string TooShort = "too-short";
    public const string TooLong = "too-long";

    private const string LocalPart = "[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+";
    private const string DomainLabel = "[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?";

    // Symfony's html5 mode, the legacy validator's: the domain needs a dot.
    private const string EmailPattern =
        "^" + LocalPart + "@" + DomainLabel + "(?:\\." + DomainLabel + ")+\\z";

    /// <summary>
    /// The submission <paramref name="request"/> makes, or null once
    /// <paramref name="errors"/> holds every rule it breaks.
    /// </summary>
    public static ContactSubmission? Read(ContactRequest request, FieldErrors errors)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(errors);
        var email = Trim(request.Email);
        var message = Trim(request.Message);
        var name = Optional(request.Name);
        var subject = Optional(request.Subject);
        var category = Trim(request.Category);
        CheckCategory(errors, category);
        CheckEmail(errors, email);
        CheckMessage(errors, message);
        CheckOptionals(errors, name, subject);
        return errors.IsEmpty
            ? new ContactSubmission
            {
                Category = category,
                Name = name,
                Email = email,
                Subject = subject,
                Message = message,
                Locale = UiLocales.TryParse(Trim(request.Locale), out var locale)
                    ? locale
                    : null,
            }
            : null;
    }

    private static void CheckCategory(FieldErrors errors, string category)
    {
        if (category.Length == 0)
        {
            errors.Add(ContactFields.Category, FieldErrors.Required);
        }
        else if (!ContactCategories.IsKnown(category))
        {
            errors.Add(ContactFields.Category, Invalid);
        }
    }

    private static void CheckEmail(FieldErrors errors, string email)
    {
        if (email.Length == 0)
        {
            errors.Add(ContactFields.Email, FieldErrors.Required);
        }
        else if (Length(email) > MaxEmailLength)
        {
            errors.Add(ContactFields.Email, TooLong);
        }
        else if (!EmailSyntax().IsMatch(email))
        {
            errors.Add(ContactFields.Email, Invalid);
        }
    }

    private static void CheckMessage(FieldErrors errors, string message)
    {
        var length = Length(message);
        if (length == 0)
        {
            errors.Add(ContactFields.Message, FieldErrors.Required);
        }
        else if (length < MinMessageLength)
        {
            errors.Add(ContactFields.Message, TooShort);
        }
        else if (length > MaxMessageLength)
        {
            errors.Add(ContactFields.Message, TooLong);
        }
    }

    // Refused when too long, as the legacy form did, rather than cut.
    private static void CheckOptionals(FieldErrors errors, string? name, string? subject)
    {
        if (Length(name) > MaxNameLength)
        {
            errors.Add(ContactFields.Name, TooLong);
        }

        if (Length(subject) > MaxSubjectLength)
        {
            errors.Add(ContactFields.Subject, TooLong);
        }
    }

    // A blank optional field is no field.
    private static string? Optional(string? value) =>
        Trim(value) is { Length: > 0 } text ? text : null;

    private static string Trim(string? value) => (value ?? string.Empty).Trim();

    private static int Length(string? text) => text?.EnumerateRunes().Count() ?? 0;

    [GeneratedRegex(EmailPattern, RegexOptions.CultureInvariant)]
    private static partial Regex EmailSyntax();
}
