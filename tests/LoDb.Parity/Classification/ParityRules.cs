using System.Text.Json;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Classification;

/// <summary>
/// The classification of the lot 1 parity run, narrowest rules first. Each rule is argued
/// in docs/reecriture/rapports/parite-lot-1.md under its id.
/// </summary>
public static class ParityRules
{
    public static IReadOnlyList<DeviationRule> All { get; } =
    [
        new()
        {
            Id = "legacy-empty-detail-in-fallback-language",
            Class = DeviationClass.LegacyDefect,
            Justification = "For a language Data Dragon lacks, the legacy stack falls back to "
                + "en_US for datasets but stores an empty champion detail: its page loses "
                + "passive, spells and skins. The new stack falls back for details too.",
            Matches = static d => d.Site.Resource == "champions"
                && Is(d, DeviationTags.Detailed) && Is(d, DeviationTags.Fallback)
                && d.Legacy is null or "null"
                && (d.Field == "passive" || d.Field.StartsWith("spells[", StringComparison.Ordinal)
                    || d.Field.StartsWith("skins[", StringComparison.Ordinal)),
        },
        new()
        {
            Id = "name-trimmed",
            Class = DeviationClass.LegacyDefect,
            Justification = "The new stack trims every display name (DdragonText.PlainName); the "
                + "legacy stack only reduces marked-up names and keeps the blanks Data Dragon "
                + "ships around a plain one.",
            Matches = static d => d.Kind == DeviationKind.Value && d.Field == "name"
                && Trimmed(d.Legacy) is { } legacy && legacy == Trimmed(d.Next),
        },
        new()
        {
            Id = "legacy-image-not-fetched",
            Class = DeviationClass.Expected,
            Justification = "The legacy stack has not fetched the image yet (pending): it warms "
                + "images of the first language's browsable entries and fetches the rest on "
                + "demand. The blob of every image is compared by the manifest rules.",
            Matches = static d => d.Site.Language is not null && d.Kind == DeviationKind.Value
                && (d.Field == "image" || d.Field.EndsWith(".image", StringComparison.Ordinal))
                && d.Legacy == "pending"
                && d.Next?.StartsWith("present ", StringComparison.Ordinal) == true,
        },
        new()
        {
            Id = "legacy-ability-icons-on-visit",
            Class = DeviationClass.Expected,
            Justification = "The legacy stack stores passive and ability icons when a detail page "
                + "is visited; the new stack ingests every image of a version. Icons of visited "
                + "champions are compared and not covered by this rule.",
            Matches = static d => d.Site.Resource == "manifest/champion"
                && d.Kind == DeviationKind.OnlyInNext && Is(d, DeviationTags.Ability)
                && !Is(d, DeviationTags.Detailed) && !Is(d, DeviationTags.Portrait),
        },
        new()
        {
            Id = "legacy-debris-images-not-warmed",
            Class = DeviationClass.Expected,
            Justification = "The legacy warmup skips the items its browsable collection leaves "
                + "out (debris); the new stack ingests their image as for any item.",
            Matches = static d => d.Site.Resource == "manifest/item"
                && d.Kind == DeviationKind.OnlyInNext && Is(d, DeviationTags.Unlisted),
        },
    ];

    private static bool Is(Deviation deviation, string tag) => deviation.Tags.Contains(tag);

    // A rendered JSON string without its blanks, or null for anything else.
    private static string? Trimmed(string? rendered)
    {
        if (rendered is null || !rendered.StartsWith('"'))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string>(rendered)?.Trim();
    }
}
