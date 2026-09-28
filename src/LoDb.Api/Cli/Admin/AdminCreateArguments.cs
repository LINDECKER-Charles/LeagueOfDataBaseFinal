using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;

namespace LoDb.Api.Cli.Admin;

/// <summary>The options of <c>admin create</c>, read from its arguments.</summary>
/// <remarks>
/// The e-mail comes as <c>--email value</c> or <c>--email=value</c>. A <c>--key=value</c>
/// whose key holds a colon is a host setting (<c>--ConnectionStrings:LoDb=…</c>), which the
/// host reads and the parser skips; any other argument is refused.
/// </remarks>
internal sealed record AdminCreateArguments
{
    public const string Usage = "Usage: admin create --email <address>";

    private const string EmailOption = "--email";
    private const string EmailAssignment = EmailOption + "=";
    private const string Prefix = "--";
    private const char Assignment = '=';
    private const char SettingSection = ':';

    /// <summary>The e-mail in its stored form: trimmed and lowercase.</summary>
    public required string Email { get; init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static AdminCreateArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string? email = null;
        for (var index = 0; index < arguments.Count; index++)
        {
            var token = arguments[index];
            var value = token switch
            {
                EmailOption when index + 1 < arguments.Count => arguments[++index],
                EmailOption => throw new FormatException("--email needs a value."),
                _ when token.StartsWith(EmailAssignment, StringComparison.Ordinal) =>
                    token[EmailAssignment.Length..],
                _ when IsHostSetting(token) => null,
                _ => throw new FormatException($"Unexpected argument '{token}'."),
            };
            if (value is not null && email is not null)
            {
                throw new FormatException("--email is given twice.");
            }

            email ??= value;
        }

        return new AdminCreateArguments { Email = Checked(email) };
    }

    private static bool IsHostSetting(string token) =>
        token.StartsWith(Prefix, StringComparison.Ordinal)
        && token.IndexOf(Assignment, StringComparison.Ordinal) is var assignment and > 0
        && token[..assignment].Contains(SettingSection, StringComparison.Ordinal);

    private static string Checked(string? email)
    {
        var stored = RegistrationRules.Email(email);
        var errors = new FieldErrors();
        RegistrationRules.CheckEmail(errors, stored);
        return errors.IsEmpty
            ? stored
            : throw new FormatException("--email must be a valid e-mail address.");
    }
}
