using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Derived.Items;
using EntityPath = LoDb.Domain.Paths.CanonicalPath;

namespace LoDb.Api.Modules.Catalog.Items.Details;

/// <summary>An item of a recipe tree, with the components it is built from.</summary>
internal sealed record RecipeStep
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string CanonicalPath { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Total cost of the item.</summary>
    public required int Gold { get; init; }

    /// <summary>What completing it costs on top of its components.</summary>
    public required int Combine { get; init; }

    public required IReadOnlyList<RecipeStep> Components { get; init; }

    public static RecipeStep Of(RecipeNode node, ItemPage page, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(images);
        var item = page.Catalog.Items.Find(node.Id);
        return new RecipeStep
        {
            Id = node.Id,
            Name = node.Name,
            CanonicalPath = item is null
                ? EntityPath.Item(node.Id, null).Value
                : page.Catalog.PathOf(item).Value,
            Image = images.Of(EntityImages.ItemIcon(node.Image)),
            Gold = node.Gold,
            Combine = node.Combine,
            Components = [.. node.Children.Select(child => Of(child, page, images))],
        };
    }
}
