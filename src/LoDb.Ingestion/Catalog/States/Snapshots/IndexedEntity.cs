using LoDb.Domain.Catalog;
using LoDb.Domain.States;

namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>An entity as the index finds it: its resource and its id, in any case.</summary>
/// <param name="Resource">Its resource.</param>
/// <param name="Id">
/// Its id as a URL or a catalog gives it (<c>FiddleSticks</c>, <c>1036</c>, <c>8000</c>).
/// </param>
public sealed record IndexedEntity(ResourceType Resource, string Id)
{
    /// <summary>The key the index holds it under.</summary>
    public string Key => EntityKey.Of(Id);
}
