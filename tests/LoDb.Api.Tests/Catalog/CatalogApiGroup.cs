namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The catalog tests share one database: ingesting the latest patch whole is the expensive
/// part. A test that warms a cold version must not rely on another having done it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CatalogApiGroup : ICollectionFixture<CatalogApiFixture>
{
    public const string Name = "Catalog API";
}
