namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>The (version, language) a call names, as received: not checked yet.</summary>
internal sealed record CatalogScope(string? Version, string? Language);
