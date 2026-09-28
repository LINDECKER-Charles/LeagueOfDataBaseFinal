using System.Globalization;
using LoDb.Api.Modules.Builds.Views;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Builds.Editing;
using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Builds.Rendering;

/// <summary>
/// Builds rendered on one patch: what it names of their champions, runes and items, their
/// images resolved in one query. Whatever the patch lacks is a ghost, its id for a name.
/// </summary>
/// <remarks>
/// Without a catalog, every entry is a ghost: a page of builds still answers when Data
/// Dragon cannot, as the legacy page did.
/// </remarks>
internal sealed class BuildScene
{
    private static readonly IReadOnlyList<int> MinorSlots =
    [
        .. Enumerable.Range(
            BuildLimits.FirstMinorSlot,
            BuildLimits.PrimaryPicks - BuildLimits.FirstMinorSlot),
    ];

    private readonly CatalogSnapshot? _catalog;
    private readonly ImageSet _images;

    private BuildScene(CatalogSnapshot? catalog, ImageSet images)
    {
        _catalog = catalog;
        _images = images;
    }

    /// <param name="context">The patch to render on; null when it could not be read.</param>
    /// <param name="structures">The builds the answer shows.</param>
    /// <param name="cancellationToken">Aborts the wait, never a shared ingestion.</param>
    public static async Task<BuildScene> OpenAsync(
        CatalogContext? context,
        IEnumerable<BuildStructure> structures,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(structures);
        if (context is null)
        {
            return new BuildScene(null, ImageSet.Empty);
        }

        var catalog = context.Catalog;
        var images = await context.ResolveAsync(
            structures.SelectMany(structure => ImagesOf(catalog, structure)).Distinct(),
            ColdDemand.Synchronous,
            cancellationToken);
        return new BuildScene(catalog, images);
    }

    public ChampionView Champion(string id)
    {
        var champion = _catalog?.Champions.Find(id);
        return champion is null
            ? new ChampionView { Id = id, Name = id, Image = CatalogImage.Absent, Missing = true }
            : new ChampionView
            {
                Id = id,
                Name = champion.Summary.Name,
                Title = champion.Summary.Title,
                Image = _images.Of(EntityImages.Portrait(champion)),
                Missing = false,
            };
    }

    public PerkView Keystone(RunePage runes)
    {
        ArgumentNullException.ThrowIfNull(runes);
        return PrimaryPick(TreeOf(runes.PrimaryStyleId), runes, BuildLimits.KeystoneSlot);
    }

    public RunePageView Runes(RunePage runes)
    {
        ArgumentNullException.ThrowIfNull(runes);
        var primary = TreeOf(runes.PrimaryStyleId);
        var secondary = TreeOf(runes.SecondaryStyleId);
        return new RunePageView
        {
            Primary = RuneViews.Path(primary, runes.PrimaryStyleId, _images),
            Keystone = PrimaryPick(primary, runes, BuildLimits.KeystoneSlot),
            Minors = [.. MinorSlots.Select(slot => PrimaryPick(primary, runes, slot))],
            Secondary = RuneViews.Path(secondary, runes.SecondaryStyleId, _images),
            SecondaryPerks =
                [.. runes.SecondarySelections.Select(id => RuneViews.Perk(secondary, id, _images))],
        };
    }

    public IReadOnlyList<StepView> Steps(IReadOnlyList<BuildStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return [.. steps.Select(step => new StepView
        {
            Label = step.Label,
            Note = step.Note,
            Items = [.. step.Items.Select(Item)],
            Gold = PurchaseGold.OfStep(step, GoldOf),
        })];
    }

    public int TotalGold(IReadOnlyList<BuildStep> steps) => PurchaseGold.OfBuild(steps, GoldOf);

    public ItemView Item(string id)
    {
        var item = _catalog?.Items.Find(id);
        return item is null
            ? new ItemView { Id = id, Name = id, Image = CatalogImage.Absent, Missing = true }
            : new ItemView
            {
                Id = id,
                Name = item.Name,
                Image = _images.Of(EntityImages.Icon(item)),
                Gold = item.Gold.Total,
                Missing = false,
            };
    }

    private static IEnumerable<DdragonImage?> ImagesOf(
        CatalogSnapshot catalog,
        BuildStructure structure)
    {
        var champion = catalog.Champions.Find(structure.ChampionId);
        int[] paths = [structure.Runes.PrimaryStyleId, structure.Runes.SecondaryStyleId];
        var trees = paths.Select(id => TreeOf(catalog, id)).OfType<RuneTree>();
        var items = structure.Steps.SelectMany(static step => step.Items)
            .Select(catalog.Items.Find)
            .OfType<Item>();
        return
        [
            champion is null ? null : EntityImages.Portrait(champion),
            .. trees.SelectMany(TreeImages),
            .. items.Select(EntityImages.Icon),
        ];
    }

    private static IEnumerable<DdragonImage?> TreeImages(RuneTree tree) =>
        [EntityImages.Icon(tree), .. tree.Slots.SelectMany(static slot => slot.Runes)
            .Select(EntityImages.Icon)];

    private static RuneTree? TreeOf(CatalogSnapshot catalog, int id) =>
        catalog.Runes.Find(id.ToString(CultureInfo.InvariantCulture));

    private RuneTree? TreeOf(int id) => _catalog is null ? null : TreeOf(_catalog, id);

    private PerkView PrimaryPick(RuneTree? tree, RunePage runes, int slot)
    {
        var picks = runes.PrimarySelections;
        return RuneViews.Perk(tree, slot < picks.Count ? picks[slot] : RunePage.Unset, _images);
    }

    private int? GoldOf(string id) => _catalog?.Items.Find(id)?.Gold.Total;
}
