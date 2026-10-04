namespace LoDb.Api.Modules.Legacy;

/// <summary>An old URL as nginx forwards it, not checked yet.</summary>
/// <param name="Path">The old path, below <c>/api/legacy</c>.</param>
/// <param name="Language">Its <c>?lang=</c>, a Data Dragon language such as fr_FR.</param>
/// <param name="Version">Its <c>?version=</c>, which a path version overrides.</param>
internal sealed record LegacyRequest(string? Path, string? Language, string? Version);
