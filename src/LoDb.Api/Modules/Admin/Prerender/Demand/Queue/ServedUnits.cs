using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>
/// What the passes wrote of some versions into the served epoch: a page is in the store once a
/// unit of its (version, locale) over its type completed, or walked past it. The patch page,
/// of no type, only by a unit over every type.
/// </summary>
/// <remarks>
/// Whatever the pass's state: a cancelled pass keeps what it wrote. A page a completed unit
/// failed counts as written: the admin relaunches the failures of a pass.
/// </remarks>
internal sealed class ServedUnits
{
    /// <summary>Why the queue skips a page a pass wrote.</summary>
    public const string AlreadyStored = "A pass already stored it in this epoch.";

    private readonly ILookup<(string Version, string Locale), ServedUnit> _units;

    private ServedUnits(IEnumerable<ServedUnit> units) =>
        _units = units.ToLookup(static unit => (unit.Version, unit.Locale));

    /// <summary>The units of <paramref name="versions"/> in <paramref name="epoch"/>.</summary>
    public static async Task<ServedUnits> ReadAsync(
        LoDbDbContext db,
        int epoch,
        IReadOnlyCollection<string> versions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var rows = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => unit.Pass!.Epoch == epoch && versions.Contains(unit.Version))
            .Select(static unit => new
            {
                unit.Version,
                unit.Locale,
                unit.State,
                unit.Cursor,
                unit.Pass!.Scope,
            })
            .ToListAsync(cancellationToken);
        return new ServedUnits(rows.Select(static row => new ServedUnit(
            row.Version,
            row.Locale,
            row.State == PrerenderUnitState.Completed,
            row.Cursor,
            PassScopeJson.Read(row.Scope).PageTypes)));
    }

    /// <summary>Whether a pass already stored <paramref name="page"/> in the epoch.</summary>
    public bool Holds(DemandPath page, VersionPageIndex index)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(index);
        return _units[(page.Version.Value, page.LocaleCode)].Any(unit =>
            (unit.Types is null || (page.Type is { } type && unit.Types.Contains(type)))
            && (unit.IsCompleted || index.IndexIn(unit.Types, page.PagePath) is var at
                && at >= 0 && at < unit.Cursor));
    }

    private sealed record ServedUnit(
        string Version,
        string Locale,
        bool IsCompleted,
        int Cursor,
        IReadOnlyList<ResourceType>? Types);
}
