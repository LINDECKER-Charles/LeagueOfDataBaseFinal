namespace LoDb.Domain.Derived.Items;

/// <summary>
/// One item of a recipe tree, with its components expanded below it.
/// </summary>
public sealed record RecipeNode
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Icon file name, resolved to an image by the caller.</summary>
    public required string Image { get; init; }

    /// <summary>Full price of the item.</summary>
    public required int Gold { get; init; }

    /// <summary>Price paid on top of the components.</summary>
    public required int Combine { get; init; }

    /// <summary>Components in recipe order; empty for a base item.</summary>
    public required IReadOnlyList<RecipeNode> Children { get; init; }
}
