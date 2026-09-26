using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Runes;

namespace LoDb.Api.Modules.Catalog.Pickers.Options;

/// <summary>A rune path and its runes, slot by slot, keystones first.</summary>
internal sealed record RuneTreeOption
{
    public required int Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>The runes of each slot, in the upstream order.</summary>
    public required IReadOnlyList<IReadOnlyList<RuneOption>> Slots { get; init; }

    public static RuneTreeOption Of(RuneTree tree, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(images);
        return new RuneTreeOption
        {
            Id = tree.Id,
            Key = tree.Key,
            Name = tree.Name,
            Image = images.Of(EntityImages.Icon(tree)),
            Slots = [.. tree.Slots.Select(slot => (IReadOnlyList<RuneOption>)
                [.. slot.Runes.Select(rune => RuneOption.Of(rune, images))])],
        };
    }
}
