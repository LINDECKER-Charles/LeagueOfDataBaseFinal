namespace LoDb.Api.Tests.Builds.Support;

/// <summary>
/// The build tests share one database: ingesting the latest patch whole is the expensive
/// part. Each test works on accounts and builds of its own.
/// </summary>
[CollectionDefinition(Name)]
public sealed class BuildsGroup : ICollectionFixture<BuildsApp>
{
    public const string Name = "Builds API";
}
