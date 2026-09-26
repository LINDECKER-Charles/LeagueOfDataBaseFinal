using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Items;

/// <summary>
/// The recipe of an item, expanded down to its base components in recipe order.
/// </summary>
public sealed class RecipeTreeTests
{
    private static readonly Dictionary<string, Item> Items = new[]
    {
        Recipe("3078", "Trinity Force", "3057", "3044") with
        {
            Gold = ItemSamples.Gold(total: 3333, combine: 333),
        },
        Recipe("3057", "Sheen", "1036", "1027"),
        Recipe("3044", "Phage", "1036", "1028"),
        ItemSamples.Named("1036", "Long Sword"),
        ItemSamples.Named("1027", "Sapphire Crystal"),
        ItemSamples.Named("1028", "Ruby Crystal"),
    }.ToDictionary(item => item.Id);

    [Fact]
    public void ComponentsExpandInRecipeOrder()
    {
        var tree = RecipeTree.Build("3078", Items);

        Assert.NotNull(tree);
        Assert.Equal(["Sheen", "Phage"], tree.Children.Select(node => node.Name));
        Assert.Equal(
            ["Long Sword", "Sapphire Crystal"],
            tree.Children[0].Children.Select(node => node.Name));
        Assert.Equal(
            ["Long Sword", "Ruby Crystal"],
            tree.Children[1].Children.Select(node => node.Name));
    }

    [Fact]
    public void ANodeCarriesItsPriceAndIcon()
    {
        var tree = RecipeTree.Build("3078", Items);

        Assert.NotNull(tree);
        Assert.Equal(3333, tree.Gold);
        Assert.Equal(333, tree.Combine);
        Assert.Equal("3078.png", tree.Image);
        Assert.Empty(tree.Children[0].Children[0].Children);
    }

    [Fact]
    public void AnAbsentRootHasNoTree()
    {
        Assert.Null(RecipeTree.Build("9999", Items));
    }

    [Fact]
    public void ComponentsTheDatasetLacksAreDropped()
    {
        var items = new[] { Recipe("3078", "Trinity Force", "3057", "0000") }
            .ToDictionary(item => item.Id);

        var tree = RecipeTree.Build("3078", items);

        Assert.NotNull(tree);
        Assert.Empty(tree.Children);
    }

    [Fact]
    public void ACycleIsCutOnItsPath()
    {
        var items = new[] { Recipe("1", "Ouroboros", "2"), Recipe("2", "Tail", "1") }
            .ToDictionary(item => item.Id);

        var tree = RecipeTree.Build("1", items);

        Assert.NotNull(tree);
        Assert.Empty(Assert.Single(tree.Children).Children);
    }

    [Fact]
    public void ThePathStopsBelowTheMaximumDepth()
    {
        var chain = Enumerable.Range(0, 10)
            .Select(level => Recipe($"{level}", $"Level {level}", $"{level + 1}"))
            .ToDictionary(item => item.Id);

        var node = RecipeTree.Build("0", chain);
        var depth = 0;
        while (node is { Children.Count: > 0 })
        {
            node = node.Children[0];
            depth++;
        }

        Assert.Equal(RecipeTree.MaxDepth, depth);
    }

    private static Item Recipe(string id, string name, params string[] from) =>
        ItemSamples.Named(id, name) with { From = from };
}
