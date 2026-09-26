namespace LoDb.Api.Tests.Legacy;

/// <summary>The redirect tests share one database: ingesting the latest patch costs.</summary>
[CollectionDefinition(Name)]
public sealed class LegacyApiGroup : ICollectionFixture<LegacyApiFixture>
{
    public const string Name = "Legacy API";
}
