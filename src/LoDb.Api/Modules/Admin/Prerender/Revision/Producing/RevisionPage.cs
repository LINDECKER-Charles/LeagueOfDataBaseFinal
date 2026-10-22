namespace LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

/// <summary>A page of a closed state to render again: its path on the site and its parts.</summary>
/// <param name="Path">The page on the site: <c>/fr/15.13.1/items/1036-long-sword</c>.</param>
/// <param name="Version">Its version, as Data Dragon spells it.</param>
/// <param name="Locale">Its locale, as the URLs spell it: <c>fr</c>.</param>
internal sealed record RevisionPage(string Path, string Version, string Locale);
