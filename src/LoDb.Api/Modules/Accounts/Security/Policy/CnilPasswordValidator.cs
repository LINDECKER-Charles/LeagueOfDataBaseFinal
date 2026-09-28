using System.Text;
using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Security.Policy;

/// <summary>
/// The CNIL policy of the legacy stack (deliberation 2022-100, a password alone): one error
/// per unmet rule, in the legacy order, with the legacy message keys as codes.
/// </summary>
/// <remarks>
/// A special character is any one neither a letter nor a number, as the legacy stack and its
/// client count them. The cap is the hasher's, 4096 bytes of UTF-8: the legacy form counted
/// characters, but its hasher refuses longer passwords, which could never sign in there.
/// </remarks>
internal sealed class CnilPasswordValidator : IPasswordValidator<User>
{
    public const int MinLength = 12;

    public const string TooLong = "auth.password.too_long";
    public const string RuleLength = "auth.password.rule_length";
    public const string RuleLowercase = "auth.password.rule_lowercase";
    public const string RuleUppercase = "auth.password.rule_uppercase";
    public const string RuleDigit = "auth.password.rule_digit";
    public const string RuleSpecial = "auth.password.rule_special";
    public const string TooCommon = "auth.password.too_common";

    private static readonly Rule[] Rules =
    [
        new(RuleLength, "Use at least 12 characters.", HasMinLength),
        new(RuleLowercase, "Use a lowercase letter.", static p => AnyRune(p, Rune.IsLower)),
        new(RuleUppercase, "Use an uppercase letter.", static p => AnyRune(p, Rune.IsUpper)),
        new(RuleDigit, "Use a digit.", static p => p.Any(char.IsAsciiDigit)),
        new(RuleSpecial, "Use a special character.", static p => AnyRune(p, IsSpecial)),
        new(TooCommon, "This password is too common.", static p => !CommonPasswords.Contains(p)),
    ];

    private static readonly IdentityError TooLongError = new()
    {
        Code = TooLong,
        Description = $"Use at most {PasswordInput.MaxBytes} bytes.",
    };

    // An empty password breaks every rule but the last: Identity has no NotBlank before it.
    public Task<IdentityResult> ValidateAsync(
        UserManager<User> manager,
        User user,
        string? password) =>
        Task.FromResult(Validate(password ?? string.Empty));

    public static IdentityResult Validate(string password)
    {
        if (!PasswordInput.FitsSymfony(password))
        {
            return IdentityResult.Failed(TooLongError);
        }

        var errors = Rules
            .Where(rule => !rule.IsMet(password))
            .Select(static rule => new IdentityError { Code = rule.Code, Description = rule.Text })
            .ToArray();
        return errors.Length == 0 ? IdentityResult.Success : IdentityResult.Failed(errors);
    }

    // Code points, as mb_strlen counts them: an emoji is one character.
    private static bool HasMinLength(string password) =>
        password.EnumerateRunes().Count() >= MinLength;

    private static bool AnyRune(string password, Func<Rune, bool> test) =>
        password.EnumerateRunes().Any(test);

    private static bool IsSpecial(Rune rune) => !Rune.IsLetter(rune) && !Rune.IsNumber(rune);

    private sealed record Rule(string Code, string Text, Func<string, bool> IsMet);
}
