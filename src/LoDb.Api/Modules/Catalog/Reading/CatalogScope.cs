using LoDb.Ingestion.Catalog;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>The (version, language) a call names, as received: not checked yet.</summary>
internal sealed record CatalogScope(string? Version, string? Language)
{
    /// <summary>
    /// Who asks, as the endpoint read it from the connection (<see cref="ColdCallers"/>):
    /// decides how a catalog no memory holds is admitted.
    /// </summary>
    public ColdCaller Caller { get; init; } = ColdCaller.Public;
}
