namespace LoDb.Domain.Builds.Rules;

/// <summary>
/// The codes of a refused build, those of the legacy stack: the front shows each one through
/// its <c>build.error.{code}</c> translation.
/// </summary>
public static class BuildErrors
{
    public const string NameLength = "name.length";
    public const string DescriptionLength = "description.length";
    public const string ModeUnknown = "mode.unknown";
    public const string VersionUnknown = "version.unknown";
    public const string LanguageUnknown = "language.unknown";

    /// <summary>A structure, or a part of it, that does not have the expected shape.</summary>
    public const string StructureInvalid = "structure.invalid";

    public const string ChampionUnknown = "champion.unknown";

    public const string PrimaryStyle = "runes.primary_style";
    public const string PrimaryCount = "runes.primary_selection_count";
    public const string PrimarySlot = "runes.primary_selection_slot";
    public const string SecondaryStyle = "runes.secondary_style";
    public const string SecondarySameStyle = "runes.secondary_same_style";
    public const string SecondaryCount = "runes.secondary_selection_count";
    public const string SecondarySlot = "runes.secondary_selection_slot";
    public const string SecondarySameSlot = "runes.secondary_same_slot";

    public const string StepsCount = "steps.count";
    public const string StepLabel = "steps.label";
    public const string StepNote = "steps.note";
    public const string StepItemsCount = "steps.items_count";
    public const string StepItemUnknown = "steps.item_unknown";
    public const string StepsTotalItems = "steps.total_items";

    /// <summary>Items of the patch the build's map does not offer, named by the error.</summary>
    public const string ItemMode = "steps.item_mode";
}
