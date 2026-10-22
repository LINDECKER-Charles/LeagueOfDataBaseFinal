using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

namespace LoDb.Api.Tests.Prerender.Revision;

/// <summary>
/// The page of an entity as a version spells it, for a path the index of states keeps as the
/// state's opener spelled it: the id's case and the slug of the version, the page itself when
/// listed, nothing for an entity the version does not list.
/// </summary>
public sealed class PageSpellingTests
{
    private static readonly VersionPageIndex Pages = new(
        ["champions", "changes", "champions/Fiddlesticks", "items/1036-long-sword"],
        new HashSet<string>());

    [Theory]
    [InlineData("champions/Fiddlesticks", "champions/Fiddlesticks")]
    [InlineData("champions/FiddleSticks", "champions/Fiddlesticks")]
    [InlineData("items/1036-longsword", "items/1036-long-sword")]
    [InlineData("changes", "changes")]
    public void PageIsSpelledAsTheVersionListsIt(string asked, string listed) =>
        Assert.Equal(listed, Pages.SpellingOf(asked));

    [Theory]
    [InlineData("champions/Aatrox")]
    [InlineData("items/3001-abyssal-mask")]
    [InlineData("runes")]
    public void EntityTheVersionDoesNotListHasNoSpelling(string asked) =>
        Assert.Null(Pages.SpellingOf(asked));
}
