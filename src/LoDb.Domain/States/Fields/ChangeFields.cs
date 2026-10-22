using System.Collections.Frozen;

namespace LoDb.Domain.States.Fields;

/// <summary>
/// The template of every <see cref="ChangeField"/>: the one table the projections, the index
/// keys, the diff engine and the API read.
/// </summary>
/// <remarks>
/// A pattern is fixed once stored: <c>catalog_entity_state.changed_fields</c> holds keys
/// written with it. Changing one is a new fingerprint scheme and a rebuild of the index.
/// </remarks>
public static class ChangeFields
{
    private const string Spell = "spell.*.";
    private const string Rune = "rune.*.";

    /// <summary>Every template, in the order of <see cref="ChangeField"/>.</summary>
    public static IReadOnlyList<FieldTemplate> All { get; } =
    [
        Value(ChangeField.Name, "name", ValueKind.Text),
        Text(ChangeField.Description, "description"),
        Value(ChangeField.Title, "title", ValueKind.Text),
        Text(ChangeField.Lore, "lore"),
        List(ChangeField.Roles, "roles", ValueKind.Code),
        Value(ChangeField.Resource, "resource", ValueKind.Text),
        OnRating(ChangeField.Rating, "rating.*"),
        OnStat(ChangeField.Stat, "stat.*", ValueKind.Number),
        OnStat(ChangeField.StatPerLevel, "statPerLevel.*", ValueKind.Number),
        Text(ChangeField.AllyTips, "allyTips"),
        Text(ChangeField.EnemyTips, "enemyTips"),
        Value(ChangeField.PassiveName, "passive.name", ValueKind.Text),
        Text(ChangeField.PassiveDescription, "passive.description"),
        OnSpell(ChangeField.SpellName, "name", ValueKind.Text),
        SpellText(ChangeField.SpellDescription, "description"),
        OnSpell(ChangeField.SpellCooldown, "cooldown", ValueKind.Text),
        OnSpell(ChangeField.SpellCost, "cost", ValueKind.Text),
        OnSpell(ChangeField.SpellRange, "range", ValueKind.Text),
        OnSpell(ChangeField.SpellMaxRank, "maxRank", ValueKind.Number),
        OnSpell(ChangeField.SpellCharges, "charges", ValueKind.Number),
        Set(ChangeField.Skins, "skins", ValueKind.Entity),
        OnSkin(ChangeField.SkinName, "skin.*.name"),
        Text(ChangeField.Plaintext, "plaintext"),
        Value(ChangeField.GoldTotal, "gold.total", ValueKind.Number),
        Value(ChangeField.GoldBase, "gold.base", ValueKind.Number),
        Value(ChangeField.GoldSell, "gold.sell", ValueKind.Number),
        Value(ChangeField.Purchasable, "gold.purchasable", ValueKind.Flag),
        List(ChangeField.Recipe, "recipe", ValueKind.Entity),
        OnStat(ChangeField.StatPercent, "statPercent.*", ValueKind.Percent),
        Set(ChangeField.Maps, "maps", ValueKind.Code),
        Set(ChangeField.Tags, "tags", ValueKind.Code),
        Value(ChangeField.Depth, "depth", ValueKind.Number),
        Value(ChangeField.Consumable, "consumable", ValueKind.Flag),
        Value(ChangeField.RequiredChampion, "requiredChampion", ValueKind.Entity),
        Value(ChangeField.RequiredAlly, "requiredAlly", ValueKind.Entity),
        RuneRow(),
        OnRune(ChangeField.RuneName, "name", ValueKind.Text),
        RuneText(ChangeField.RuneShortDesc, "shortDesc"),
        RuneText(ChangeField.RuneLongDesc, "longDesc"),
        List(ChangeField.Cooldown, "cooldown", ValueKind.Number),
        List(ChangeField.Cost, "cost", ValueKind.Number),
        Value(ChangeField.CostType, "costType", ValueKind.Text),
        List(ChangeField.Range, "range", ValueKind.Range),
        Value(ChangeField.SummonerLevel, "summonerLevel", ValueKind.Number),
        Value(ChangeField.Charges, "charges", ValueKind.Number),
        Set(ChangeField.Modes, "modes", ValueKind.Code),
    ];

    private static readonly FrozenDictionary<ChangeField, FieldTemplate> ByField =
        All.ToFrozenDictionary(static template => template.Field);

    private static readonly FrozenDictionary<string, FieldTemplate> ByPattern =
        All.ToFrozenDictionary(static template => template.Pattern, StringComparer.Ordinal);

    /// <summary>The template of <paramref name="field"/>.</summary>
    public static FieldTemplate Of(ChangeField field) => ByField[field];

    /// <summary>The template whose pattern is <paramref name="pattern"/>, if any.</summary>
    public static FieldTemplate? WithPattern(string pattern) =>
        ByPattern.GetValueOrDefault(pattern);

    private static FieldTemplate Value(ChangeField field, string pattern, ValueKind values) =>
        new() { Field = field, Pattern = pattern, Kind = FieldKind.Value, Values = values };

    private static FieldTemplate Text(ChangeField field, string pattern) =>
        Value(field, pattern, ValueKind.Text) with { Kind = FieldKind.Text };

    private static FieldTemplate List(ChangeField field, string pattern, ValueKind values) =>
        Value(field, pattern, values) with { Kind = FieldKind.List };

    private static FieldTemplate Set(ChangeField field, string pattern, ValueKind values) =>
        Value(field, pattern, values) with { Kind = FieldKind.Set };

    // A template with a subject holds one value of the subject.
    private static FieldTemplate OnRating(ChangeField field, string pattern) =>
        Value(field, pattern, ValueKind.Number) with { Subject = SubjectKind.Rating };

    private static FieldTemplate OnStat(ChangeField field, string pattern, ValueKind values) =>
        Value(field, pattern, values) with { Subject = SubjectKind.Stat };

    private static FieldTemplate OnSpell(ChangeField field, string name, ValueKind values) =>
        Value(field, Spell + name, values) with { Subject = SubjectKind.SpellSlot };

    private static FieldTemplate SpellText(ChangeField field, string name) =>
        OnSpell(field, name, ValueKind.Text) with { Kind = FieldKind.Text };

    private static FieldTemplate OnSkin(ChangeField field, string pattern) =>
        Value(field, pattern, ValueKind.Text) with { Subject = SubjectKind.Skin };

    private static FieldTemplate OnRune(ChangeField field, string name, ValueKind values) =>
        Value(field, Rune + name, values) with { Subject = SubjectKind.Rune };

    private static FieldTemplate RuneText(ChangeField field, string name) =>
        OnRune(field, name, ValueKind.Text) with { Kind = FieldKind.Text };

    // The runes of a row, in the order the page shows them.
    private static FieldTemplate RuneRow() =>
        Value(ChangeField.RuneSlot, "slot.*", ValueKind.Entity)
            with { Kind = FieldKind.List, Subject = SubjectKind.RuneSlot };
}
