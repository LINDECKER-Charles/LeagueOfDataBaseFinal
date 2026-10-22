using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;

namespace LoDb.Api.Tests.Prerender.Pages;

/// <summary>
/// What a unit restricted to some types of page walks: each list with its entities, in their
/// order; the patch page, of no type, only in a unit over every type.
/// </summary>
public sealed class PageTypesTests
{
    private static readonly IReadOnlyList<string> Pages =
    [
        "champions", "items", "runes", "summoners", "changes", "champions/Aatrox",
        "items/1036-long-sword", "itemsets",
    ];

    [Fact]
    public void UnitOverEveryTypeWalksEveryPagePatchPageIncluded()
    {
        Assert.Same(Pages, PageTypes.Keep(Pages, null));
    }

    [Fact]
    public void TypeFilterKeepsItsListsAndEntitiesAndLeavesThePatchPageOut()
    {
        var kept = PageTypes.Keep(Pages, [ResourceType.Items, ResourceType.Champions]);

        Assert.Equal(["champions", "items", "champions/Aatrox", "items/1036-long-sword"], kept);
    }
}
