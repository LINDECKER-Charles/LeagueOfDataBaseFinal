namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>The state of one (version, locale) in an epoch.</summary>
/// <param name="Code">
/// c, w, p or f (<see cref="Http.Views.UnitCells"/>); an absent cell is not in the map.
/// </param>
/// <param name="Held">
/// The types of page held: bit 0 champions, 1 items, 2 runes, 3 summoners (the order of
/// ResourceType, <see cref="Pages.TypeMask"/>), each set when the latest completed unit over
/// that type failed no page.
/// </param>
/// <param name="Pages">The most pages any unit wrote for it (a later unit replaces them).</param>
/// <param name="Bytes">The most bytes any unit wrote for it.</param>
/// <param name="FailedPages">
/// Pages failed by the latest completed unit over each type, summed once.
/// </param>
/// <param name="LastEndedAt">When its latest unit ended, completed or failed; null if none.</param>
internal sealed record CoverageCell(
    char Code,
    int Held,
    int Pages,
    long Bytes,
    int FailedPages,
    DateTimeOffset? LastEndedAt);
