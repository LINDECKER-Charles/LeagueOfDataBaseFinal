namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// The tests of the index of states share one API, whose index each test seeds: they run one
/// at a time, and each empties what it seeded before it ends.
/// </summary>
[CollectionDefinition(Name)]
public sealed class StateApiGroup : ICollectionFixture<StateApiFixture>
{
    public const string Name = "Catalog states API";
}
