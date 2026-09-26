using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.Text;
using LoDb.Ingestion.Catalog.Hotlinks;

namespace LoDb.Api.Modules.Catalog.Champions.Details;

/// <summary>The passive or a spell of a champion, with its preview video.</summary>
internal sealed record ChampionAbility
{
    // Data Dragon lists a champion's four spells in the order of their keys.
    private static readonly AbilitySlot[] SpellSlots =
        [AbilitySlot.Q, AbilitySlot.W, AbilitySlot.E, AbilitySlot.R];

    public required AbilitySlot Slot { get; init; }

    /// <summary>Data Dragon spell id, such as AhriQ; null for the passive.</summary>
    public string? Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Riot's rich text, unresolved template tokens removed.</summary>
    public required string Description { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Per-rank cooldown, such as "7/6.5/6/5.5/5".</summary>
    public string? Cooldown { get; init; }

    public string? Cost { get; init; }

    /// <summary>Per-rank range; null when missing or a placeholder value.</summary>
    public string? Range { get; init; }

    public int? MaxRank { get; init; }

    public int? Charges { get; init; }

    /// <summary>The preview, which may not exist: the page falls back to the icon.</summary>
    public AbilityVideo? Video { get; init; }

    /// <summary>The passive, then the spells in their Q, W, E, R order.</summary>
    public static IReadOnlyList<ChampionAbility> AllOf(ChampionDetail champion, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(champion);
        ArgumentNullException.ThrowIfNull(images);
        var source = new Source(champion.Summary, images);
        List<ChampionAbility> abilities = [];
        if (champion.Passive is { } passive)
        {
            abilities.Add(Passive(passive, source));
        }

        abilities.AddRange(champion.Spells
            .Zip(SpellSlots)
            .Select(pair => Spell(pair.First, pair.Second, source)));
        return abilities;
    }

    private static ChampionAbility Passive(ChampionPassive passive, Source source) => new()
    {
        Slot = AbilitySlot.Passive,
        Name = passive.Name,
        Description = DdragonText.Clean(passive.Description),
        Image = source.Images.Of(EntityImages.Passive(passive)),
        Video = ChampionHotlinks.Video(source.Champion, AbilitySlot.Passive),
    };

    private static ChampionAbility Spell(ChampionSpell spell, AbilitySlot slot, Source source) =>
        new()
        {
            Slot = slot,
            Id = spell.Id,
            Name = spell.Name,
            Description = DdragonText.Clean(spell.Description),
            Image = source.Images.Of(EntityImages.Ability(spell)),
            Cooldown = spell.CooldownBurn,
            Cost = spell.CostBurn,
            Range = RangeSentinels.MaskAbilityRange(spell.RangeBurn),
            MaxRank = spell.MaxRank,
            Charges = spell.Charges,
            Video = ChampionHotlinks.Video(source.Champion, slot),
        };

    private sealed record Source(ChampionSummary Champion, ImageSet Images);
}
