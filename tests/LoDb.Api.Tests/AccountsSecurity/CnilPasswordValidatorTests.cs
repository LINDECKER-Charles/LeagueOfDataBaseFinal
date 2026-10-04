using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Api.Modules.Accounts.Security.Policy;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// The CNIL policy of the legacy stack, rule by rule, with its message keys in its order: the
/// cases of its own tests first, then what PHP's Unicode classes and mb_ functions decide.
/// </summary>
public sealed class CnilPasswordValidatorTests
{
    private const string ResourceName = "common-passwords.txt";
    private const int CommonPasswordCount = 997;

    // Four characters of four classes, repeated up to Symfony's 4096 bytes.
    private const string Pattern = "Zz9?";

    // 'é' takes two bytes of UTF-8.
    private const int AccentBytes = 2;

    private static readonly User Anyone = new() { Roles = [] };

    private static readonly string AtTheMaximum =
        string.Concat(Enumerable.Repeat(Pattern, PasswordInput.MaxBytes / Pattern.Length));

    private static readonly string[] EveryRule =
    [
        CnilPasswordValidator.RuleLength, CnilPasswordValidator.RuleLowercase,
        CnilPasswordValidator.RuleUppercase, CnilPasswordValidator.RuleDigit,
        CnilPasswordValidator.RuleSpecial,
    ];

    public static TheoryData<string> Compliant =>
    [
        "Str0ng-passphrase!",
        // Accented letters have a case; the dash and the spaces are special characters.
        "Épée légère — 2026",
        // Twelve code points, as mb_strlen counts: an emoji is one character, and special.
        "Aa1-💎💎💎💎💎💎💎💎",
        "Ωmega straße 9",
        AtTheMaximum,
    ];

    public static TheoryData<string, string> SingleUnmetRule => new()
    {
        // app/tests/Unit/Validator/CnilPasswordValidatorTest.php.
        { "Sh0rt-pass!", CnilPasswordValidator.RuleLength },
        { "NOLOWERCASE-123456", CnilPasswordValidator.RuleLowercase },
        { "nouppercase-123456", CnilPasswordValidator.RuleUppercase },
        { "NoDigits-Password!", CnilPasswordValidator.RuleDigit },
        { "NoSpecials12345678", CnilPasswordValidator.RuleSpecial },
        // Eleven code points in eighteen UTF-16 units.
        { "Aa1-💎💎💎💎💎💎💎", CnilPasswordValidator.RuleLength },
        { "ÀÉÎÕÜ-ΩMEGA-2026", CnilPasswordValidator.RuleLowercase },
        // A digit is 0 to 9, as [0-9] reads; other scripts' digits are numbers, not special.
        { "Abcdefghijk-٣", CnilPasswordValidator.RuleDigit },
        { "Abcdefghijk9٣", CnilPasswordValidator.RuleSpecial },
    };

    public static TheoryData<string> OverTheMaximum =>
    [
        AtTheMaximum + "!",
        // 2049 characters, but 4098 bytes.
        new string('é', (PasswordInput.MaxBytes / AccentBytes) + 1),
    ];

    [Theory]
    [MemberData(nameof(Compliant))]
    public void CompliantPasswordPasses(string password) =>
        Assert.True(CnilPasswordValidator.Validate(password).Succeeded);

    [Theory]
    [MemberData(nameof(SingleUnmetRule))]
    public void EachUnmetRuleIsReportedAlone(string password, string rule) =>
        Assert.Equal([rule], Codes(CnilPasswordValidator.Validate(password)));

    [Fact]
    public void CommonPasswordIsRefusedWhateverItsCase() =>
        Assert.Equal(
            [
                CnilPasswordValidator.RuleLength, CnilPasswordValidator.RuleSpecial,
                CnilPasswordValidator.TooCommon,
            ],
            Codes(CnilPasswordValidator.Validate("TrustNo1")));

    // Identity has no NotBlank before the validator: an empty password breaks every rule but
    // the list, in the legacy order.
    [Fact]
    public async Task EmptyPasswordBreaksEveryRuleButTheList()
    {
        var validator = new CnilPasswordValidator();

        Assert.Equal(EveryRule, Codes(await validator.ValidateAsync(null!, Anyone, null)));
        Assert.Equal(EveryRule, Codes(await validator.ValidateAsync(null!, Anyone, string.Empty)));
    }

    // Longer, the hasher could not hash it: that error alone, whatever the other rules say.
    [Theory]
    [MemberData(nameof(OverTheMaximum))]
    public void PasswordOverTheMaximumIsOnlyTooLong(string password) =>
        Assert.Equal(
            [CnilPasswordValidator.TooLong],
            Codes(CnilPasswordValidator.Validate(password)));

    [Fact]
    public void ListHoldsTheLegacyPasswordsInLowercase()
    {
        var passwords = ReadList();

        Assert.Equal(CommonPasswordCount, passwords.Count);
        Assert.Equal(CommonPasswordCount, passwords.Distinct(StringComparer.Ordinal).Count());
        Assert.All(passwords, static password =>
            Assert.Equal(password.ToLowerInvariant(), password));
    }

    [Fact]
    public void EveryListedPasswordIsTooCommonWhateverItsCase() =>
        Assert.All(ReadList(), static password =>
            Assert.True(CommonPasswords.Contains(password.ToUpperInvariant()), password));

    private static string[] Codes(IdentityResult result) =>
        [.. result.Errors.Select(static error => error.Code)];

    private static List<string> ReadList()
    {
        using var stream = typeof(CnilPasswordValidator).Assembly
            .GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        return [.. reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)];
    }
}
