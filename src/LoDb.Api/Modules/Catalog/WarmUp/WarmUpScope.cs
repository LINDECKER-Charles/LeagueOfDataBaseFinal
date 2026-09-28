using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>What a warm-up prepares: a catalog, then the images of some of its lists.</summary>
/// <param name="Catalog">The (version, language), as received: the gateway checks it.</param>
/// <param name="Resources">The lists, each once; none prepares the datasets alone.</param>
internal sealed record WarmUpScope(CatalogScope Catalog, IReadOnlyList<ResourceType> Resources);
