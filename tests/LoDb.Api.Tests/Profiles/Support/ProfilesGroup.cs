namespace LoDb.Api.Tests.Profiles.Support;

/// <summary>
/// The profile tests share one database: ingesting the latest patch whole is the expensive
/// part. Each test works on accounts of its own.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ProfilesGroup : ICollectionFixture<ProfilesApp>
{
    public const string Name = "Profiles API";
}
