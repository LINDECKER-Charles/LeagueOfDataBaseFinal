namespace LoDb.Domain.States.Fields;

/// <summary>
/// The closed set of field templates a page of an entity displays and the index compares
/// (docs/produit/seo-une-page-par-etat.md): what a state's changed fields name, and the key
/// of their i18n label.
/// </summary>
/// <remarks>
/// <para>
/// A template takes a subject when the page shows the field once per sub-entity: a spell
/// slot, a stat, a rating, a rune, a rune row or a skin (<see cref="ChangeFields"/>). Its
/// stored key is dotted (<see cref="FieldKey"/>), written in the API as its camelCase name.
/// </para>
/// <para>
/// The order is part of <see cref="Fingerprints.FingerprintScheme.Current"/>: the fingerprint
/// serializes the fields in this order. Never reorder nor rename a member; a new one that a
/// projection emits is a new scheme.
/// </para>
/// </remarks>
public enum ChangeField
{
    // Every resource.
    Name,
    Description,

    // Champions.
    Title,
    Lore,
    Roles,
    Resource,
    Rating,
    Stat,
    StatPerLevel,
    AllyTips,
    EnemyTips,
    PassiveName,
    PassiveDescription,
    SpellName,
    SpellDescription,
    SpellCooldown,
    SpellCost,
    SpellRange,
    SpellMaxRank,
    SpellCharges,
    Skins,
    SkinName,

    // Items (and Stat above).
    Plaintext,
    GoldTotal,
    GoldBase,
    GoldSell,
    Purchasable,
    Recipe,
    StatPercent,
    Maps,
    Tags,
    Depth,
    Consumable,
    RequiredChampion,
    RequiredAlly,

    // Rune paths.
    RuneSlot,
    RuneName,
    RuneShortDesc,
    RuneLongDesc,

    // Summoner spells.
    Cooldown,
    Cost,
    CostType,
    Range,
    SummonerLevel,
    Charges,
    Modes,
}
