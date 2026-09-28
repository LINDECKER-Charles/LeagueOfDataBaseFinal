using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>The (version, language) a catalog is kept under.</summary>
internal readonly record struct CatalogKey(PatchVersion Version, DdragonLanguage Language);
