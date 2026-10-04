using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Api.Modules.Accounts.Security.Policy;

namespace LoDb.Api.Cli.Admin;

/// <summary>The options of <c>admin root</c>, read from its arguments.</summary>
/// <remarks>
/// An option comes as <c>--name value</c> or <c>--name=value</c>; a <c>--key=value</c> whose
/// key holds a colon is a host setting, which the host reads and the parser skips. The
/// password is never an argument, which <c>ps</c> would show on the host: it is the first
/// line of the standard input.
/// </remarks>
internal sealed record AdminRootArguments
{
    public const string Usage =
        "Usage: admin root --username <name> --email <address>, the password on the standard input";

    private const string Prefix = "--";
    private const char Assignment = '=';
    private const char SettingSection = ':';
    private const string UsernameOption = "username";
    private const string EmailOption = "email";

    /// <summary>The username in its stored form: trimmed, its case kept.</summary>
    public required string Username { get; init; }

    /// <summary>The e-mail in its stored form: trimmed and lowercase.</summary>
    public required string Email { get; init; }

    /// <exception cref="FormatException">The arguments are not a valid use.</exception>
    public static AdminRootArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var token = arguments[index];
            if (IsHostSetting(token))
            {
                continue;
            }

            var (name, value) = OptionOf(token);
            value ??= index + 1 < arguments.Count
                ? arguments[++index]
                : throw new FormatException($"--{name} needs a value.");
            if (!options.TryAdd(name, value))
            {
                throw new FormatException($"--{name} is given twice.");
            }
        }

        return new AdminRootArguments
        {
            Username = CheckedUsername(options.GetValueOrDefault(UsernameOption)),
            Email = CheckedEmail(options.GetValueOrDefault(EmailOption)),
        };
    }

    /// <summary>The password, from the first line of the standard input.</summary>
    /// <remarks>
    /// Its length is its only rule, as for the legacy ADMIN_PASSWORD: the CNIL classes are for
    /// the passwords members choose, and an operator's long random secret goes well beyond
    /// the 80 bits of entropy deliberation 2022-100 also accepts.
    /// </remarks>
    /// <exception cref="FormatException">No usable password was given.</exception>
    public static string Password(string? line)
    {
        var password = line?.TrimEnd('\r') ?? string.Empty;
        return password.EnumerateRunes().Count() >= CnilPasswordValidator.MinLength
            && PasswordInput.FitsSymfony(password)
            ? password
            : throw new FormatException(
                $"The standard input must hold a password of {CnilPasswordValidator.MinLength}"
                + $" characters at least and {PasswordInput.MaxBytes} bytes at most.");
    }

    private static bool IsHostSetting(string token) =>
        token.StartsWith(Prefix, StringComparison.Ordinal)
        && token.IndexOf(Assignment, StringComparison.Ordinal) is var assignment and > 0
        && token[..assignment].Contains(SettingSection, StringComparison.Ordinal);

    private static (string Name, string? Value) OptionOf(string token)
    {
        var body = token.StartsWith(Prefix, StringComparison.Ordinal)
            ? token[Prefix.Length..]
            : throw new FormatException($"Unexpected argument '{token}'.");
        var assignment = body.IndexOf(Assignment, StringComparison.Ordinal);
        var name = assignment < 0 ? body : body[..assignment];
        return name is UsernameOption or EmailOption
            ? (name, assignment < 0 ? null : body[(assignment + 1)..])
            : throw new FormatException($"Unknown option '{token}'.");
    }

    private static string CheckedUsername(string? username)
    {
        var stored = RegistrationRules.Username(username);
        return RegistrationRules.IsUsername(stored)
            ? stored
            : throw new FormatException("--username must be a valid username.");
    }

    private static string CheckedEmail(string? email)
    {
        var stored = RegistrationRules.Email(email);
        var errors = new FieldErrors();
        RegistrationRules.CheckEmail(errors, stored);
        return errors.IsEmpty
            ? stored
            : throw new FormatException("--email must be a valid e-mail address.");
    }
}
