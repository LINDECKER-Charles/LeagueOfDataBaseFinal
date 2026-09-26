using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Editions;

namespace LoDb.Domain.Builds.Import;

/// <summary>
/// Carries a build over to another patch as a draft: what the target patch still offers is
/// kept, the rest is dropped and reported, nothing is ever guessed.
/// </summary>
/// <remarks>
/// The rune page is kept whole or reset: a page missing one perk is no page, and swapping in
/// a lookalike would publish a choice its author never made. The champion stays even when
/// the target lacks it: the report flags it instead.
/// </remarks>
public static class BuildStructureProjector
{
    public static BuildProjection Project(
        BuildStructureInput source,
        GameMode mode,
        BuildCatalog target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var championId = source.ChampionId?.Trim() ?? string.Empty;
        var (runes, runesReset) = ProjectRunes(source.Runes, target.Runes);
        var dropped = new List<DroppedItem>();
        var steps = ProjectSteps(source.Steps ?? [], new StepTarget(mode, target, dropped));
        return new BuildProjection
        {
            Structure = new BuildStructure
            {
                ChampionId = championId,
                Runes = runes,
                Steps = steps,
            },
            Report = new ImportReport
            {
                ChampionMissing = championId.Length == 0 || !target.HasChampion(championId),
                RunesReset = runesReset,
                DroppedItems = dropped,
            },
        };
    }

    private static (RunePage Runes, bool Reset) ProjectRunes(
        RunePageInput? runes,
        RuneTreeIndex trees)
    {
        List<int> ids = [.. runes?.ReadableIds() ?? []];

        // A page never configured is blank already: nothing was reset.
        if (ids.Count == 0)
        {
            return (RunePage.Blank, false);
        }

        return ids.TrueForAll(trees.Knows)
            ? (BuildStructureNormalizer.Normalize(runes), false)
            : (RunePage.Blank, true);
    }

    private static List<BuildStep> ProjectSteps(
        IReadOnlyList<StepInput?> steps,
        StepTarget target)
    {
        var kept = new List<BuildStep>();
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index] is not { } step)
            {
                continue;
            }

            var items = target.Keep(index, step.Items ?? []);

            // A step left without items is no step: the editor refuses an empty one.
            if (items.Count > 0)
            {
                kept.Add(new BuildStep
                {
                    Label = step.Label ?? string.Empty,
                    Note = step.Note,
                    Items = items,
                });
            }
        }

        return kept;
    }

    private sealed class StepTarget(GameMode mode, BuildCatalog catalog, List<DroppedItem> dropped)
    {
        public List<string> Keep(int step, IEnumerable<string?> items)
        {
            var playable = new List<string>();
            foreach (var id in items.OfType<string>())
            {
                if (catalog.FindItem(id) is { } item && ItemPlayability.IsAvailableOn(item, mode))
                {
                    playable.Add(id);
                }
                else
                {
                    dropped.Add(new DroppedItem { Step = step, Id = id, Name = NameOf(id) });
                }
            }

            return playable;
        }

        private string NameOf(string id) =>
            ItemEdition.QualifiedName(id, catalog.FindItem(id)?.Name ?? id);
    }
}
