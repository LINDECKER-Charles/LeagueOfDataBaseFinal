namespace LoDb.Api.Tests.Analytics.Support;

/// <summary>The capture tests share one database: ingesting the latest patch costs.</summary>
[CollectionDefinition(Name)]
public sealed class AnalyticsApiGroup : ICollectionFixture<AnalyticsApiFixture>
{
    public const string Name = "Analytics API";
}
