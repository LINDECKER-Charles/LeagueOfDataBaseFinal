using LoDb.Api.Modules.Audit.Reading.Query;
using LoDb.Api.Modules.Audit.Vocabulary;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Audit.Units;

/// <summary>The groups of the admin filter, and the escaping of the actor filter.</summary>
public sealed class AuditVocabularyTests
{
    [Fact]
    public void EveryActionHasAGroup()
    {
        foreach (var action in Enum.GetValues<AuditAction>())
        {
            Assert.Contains(AuditCategories.Of(action), AuditCategories.All);
        }
    }

    [Theory]
    [InlineData(AuditAction.UserLoginFailed, "auth")]
    [InlineData(AuditAction.ProfileUpdate, "account")]
    [InlineData(AuditAction.BuildVote, "build")]
    [InlineData(AuditAction.ApiKeyRegenerate, "apikey")]
    [InlineData(AuditAction.AdminLogsPurge, "admin")]
    public void GroupsAreTheLegacyOnes(AuditAction action, string category) =>
        Assert.Equal(category, AuditCategories.Of(action));

    [Fact]
    public void UnknownGroupHasNoAction() => Assert.Empty(AuditCategories.ActionsOf("misc"));

    [Theory]
    [InlineData("Legende_42", @"Legende\_42")]
    [InlineData("100%", @"100\%")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData("plain", "plain")]
    public void LikePatternMatchesTheTextAlone(string text, string pattern) =>
        Assert.Equal(pattern, LikeEscape.Escape(text));
}
