using LoDb.Api.Modules.Profiles.Deletion;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Tests.Profiles.Units;

public sealed class DeletionRulesTests
{
    [Theory]
    [InlineData("google-id", "hash", nameof(DeletionConfirmation.Phrase))]
    [InlineData("google-id", null, nameof(DeletionConfirmation.Phrase))]
    [InlineData(null, "hash", nameof(DeletionConfirmation.Password))]
    [InlineData(null, null, nameof(DeletionConfirmation.None))]
    public void GoogleAccountsTypeAPhraseOthersTheirPassword(
        string? googleId,
        string? passwordHash,
        string expected)
    {
        var user = new User { Roles = [], GoogleId = googleId, PasswordHash = passwordHash };

        Assert.Equal(expected, DeletionRules.ConfirmationOf(user).ToString());
    }

    [Theory]
    [InlineData(UiLocale.Fr, "SUPPRIMER MON COMPTE")]
    [InlineData(UiLocale.En, "DELETE MY ACCOUNT")]
    [InlineData(UiLocale.De, "DELETE MY ACCOUNT")]
    [InlineData(UiLocale.Ja, "DELETE MY ACCOUNT")]
    public void OnlyFrenchTranslatesThePhrase(UiLocale locale, string phrase)
    {
        Assert.Equal(phrase, DeletionPhrases.For(locale));
    }

    [Theory]
    [InlineData("  supprimer mon compte ", UiLocale.Fr, true)]
    [InlineData("DELETE MY ACCOUNT", UiLocale.Fr, false)]
    [InlineData("Delete My Account", UiLocale.Es, true)]
    [InlineData("DELETE  MY ACCOUNT", UiLocale.En, false)]
    [InlineData("", UiLocale.En, false)]
    [InlineData(null, UiLocale.En, false)]
    public void TypedPhraseMatchesTrimmedWhateverItsCase(
        string? typed,
        UiLocale locale,
        bool matches)
    {
        Assert.Equal(matches, DeletionPhrases.Matches(typed, locale));
    }
}
