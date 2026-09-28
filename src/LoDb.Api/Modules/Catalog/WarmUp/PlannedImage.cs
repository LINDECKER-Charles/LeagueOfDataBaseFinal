using LoDb.Domain.Catalog;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>An image a warm-up fetches, with the list it belongs to and the entry it shows.</summary>
internal sealed record PlannedImage(ResourceType Resource, DdragonImage Image, string Name);
