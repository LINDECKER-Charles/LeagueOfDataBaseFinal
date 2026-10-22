namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The SEO tests share one database and one index of states, apart from the other tests: they
/// run one at a time, so that a test that makes the index unavailable disturbs no other.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SeoApiGroup : ICollectionFixture<SeoApiFixture>
{
    public const string Name = "SEO API";
}
