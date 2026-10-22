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
        Plain(ChangeField.Name, "name", FieldKind.Value, ValueKind.Text),
        Plain(ChangeField.Description, "description", FieldKind.Text, ValueKind.Text),
        Plain(ChangeField.Title, "title", FieldKind.Value, ValueKind.Text),
        Plain(ChangeField.Lore, "lore", FieldKind.Text, ValueKind.Text),
        Plain(ChangeField.Roles, "roles", FieldKind.List, ValueKind.Code),
        Plain(ChangeField.Resource, "resource", FieldKind.Value, ValueKind.Text),
        Of(ChangeField.Rating, "rating.*", ValueKind.Number, SubjectKind.Rating),
        Of(ChangeField.Stat, "stat.*", ValueKind.Number, SubjectKind.Stat),
        Of(ChangeField.StatPerLevel, "statPerLevel.*", ValueKind.Number, SubjectKind.Stat),
        Plain(ChangeField.AllyTips, "allyTips", FieldKind.Text, ValueKind.Text),
        Plain(ChangeField.EnemyTips, "enemyTips", FieldKind.Text, ValueKind.Text),
        Plain(ChangeField.PassiveName, "passive.name", FieldKind.Value, ValueKind.Text),
        Plain(
            ChangeField.PassiveDescription,
            "passive.description",
            FieldKind.Text,
            ValueKind.Text),
        Of(ChangeField.SpellName, Spell + "name", ValueKind.Text, SubjectKind.SpellSlot),
        SpellText(ChangeField.SpellDescription, Spell + "description"),
        Of(ChangeField.SpellCooldown, Spell + "cooldown", ValueKind.Text, SubjectKind.SpellSlot),
        Of(ChangeField.SpellCost, Spell + "cost", ValueKind.Text, SubjectKind.SpellSlot),
        Of(ChangeField.SpellRange, Spell + "range", ValueKind.Text, SubjectKind.SpellSlot),
        Of(ChangeField.SpellMaxRank, Spell + "maxRank", ValueKind.Number, SubjectKind.SpellSlot),
        Of(ChangeField.SpellCharges, Spell + "charges", ValueKind.Number, SubjectKind.SpellSlot),
        Plain(ChangeField.Skins, "skins", FieldKind.Set, ValueKind.Entity),
        Of(ChangeField.SkinName, "skin.*.name", ValueKind.Text, SubjectKind.Skin),
        Plain(ChangeField.Plaintext, "plaintext", FieldKind.Text, ValueKind.Text),
        Plain(ChangeField.GoldTotal, "gold.total", FieldKind.Value, ValueKind.Number),
        Plain(ChangeField.GoldBase, "gold.base", FieldKind.Value, ValueKind.Number),
        Plain(ChangeField.GoldSell, "gold.sell", FieldKind.Value, ValueKind.Number),
        Plain(ChangeField.Purchasable, "gold.purchasable", FieldKind.Value, ValueKind.Flag),
        Plain(ChangeField.Recipe, "recipe", FieldKind.List, ValueKind.Entity),
        Of(ChangeField.StatPercent, "statPercent.*", ValueKind.Percent, SubjectKind.Stat),
        Plain(ChangeField.Maps, "maps", FieldKind.Set, ValueKind.Code),
        Plain(ChangeField.Tags, "tags", FieldKind.Set, ValueKind.Code),
        Plain(ChangeField.Depth, "depth", FieldKind.Value, ValueKind.Number),
        Plain(ChangeField.Consumable, "consumable", FieldKind.Value, ValueKind.Flag),
        Plain(ChangeField.RequiredChampion, "requiredChampion", FieldKind.Value, ValueKind.Entity),
        Plain(ChangeField.RequiredAlly, "requiredAlly", FieldKind.Value, ValueKind.Entity),
        RuneRow(),
        Of(ChangeField.RuneName, Rune + "name", ValueKind.Text, SubjectKind.Rune),
        RuneText(ChangeField.RuneShortDesc, Rune + "shortDesc"),
        RuneText(ChangeField.RuneLongDesc, Rune + "longDesc"),
        Plain(ChangeField.Cooldown, "cooldown", FieldKind.List, ValueKind.Number),
        Plain(ChangeField.Cost, "cost", FieldKind.List, ValueKind.Number),
        Plain(ChangeField.CostType, "costType", FieldKind.Value, ValueKind.Text),
        Plain(ChangeField.Range, "range", FieldKind.List, ValueKind.Range),
        Plain(ChangeField.SummonerLevel, "summonerLevel", FieldKind.Value, ValueKind.Number),
        Plain(ChangeField.Charges, "charges", FieldKind.Value, ValueKind.Number),
        Plain(ChangeField.Modes, "modes", FieldKind.Set, ValueKind.Code),
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

    private static FieldTemplate Plain(
        ChangeField field,
        string pattern,
        FieldKind kind,
        ValueKind values) =>
        new() { Field = field, Pattern = pattern, Kind = kind, Values = values };

    // A template with a subject holds one value of the subject.
    private static FieldTemplate Of(
        ChangeField field,
        string pattern,
        ValueKind values,
        SubjectKind subject) =>
        new()
        {
            Field = field,
            Pattern = pattern,
            Kind = FieldKind.Value,
            Values = values,
            Subject = subject,
        };

    private static FieldTemplate SpellText(ChangeField field, string pattern) =>
        Of(field, pattern, ValueKind.Text, SubjectKind.SpellSlot) with { Kind = FieldKind.Text };

    private static FieldTemplate RuneText(ChangeField field, string pattern) =>
        Of(field, pattern, ValueKind.Text, SubjectKind.Rune) with { Kind = FieldKind.Text };

    // The runes of a row, in the order the page shows them.
    private static FieldTemplate RuneRow() =>
        Of(ChangeField.RuneSlot, "slot.*", ValueKind.Entity, SubjectKind.RuneSlot)
            with { Kind = FieldKind.List };
}
