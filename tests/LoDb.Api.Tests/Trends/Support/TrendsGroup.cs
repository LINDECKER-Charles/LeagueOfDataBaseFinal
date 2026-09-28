using LoDb.Api.Tests.Builds.Support;

namespace LoDb.Api.Tests.Trends.Support;

/// <summary>
/// The trends read every public build: their tests get a database of their own, so the
/// builds of the other tests never enter a ranking.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TrendsGroup : ICollectionFixture<BuildsApp>
{
    public const string Name = "Trends API";
}
