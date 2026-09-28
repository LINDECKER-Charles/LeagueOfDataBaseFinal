namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The SEO tests share one database, apart from the catalog tests: those warm cold versions
/// synchronously, which would make a pending sitemap depend on the order of the runs.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SeoApiGroup : ICollectionFixture<SeoApiFixture>
{
    public const string Name = "SEO API";
}
