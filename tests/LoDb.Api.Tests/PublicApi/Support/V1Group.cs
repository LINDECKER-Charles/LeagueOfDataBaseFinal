namespace LoDb.Api.Tests.PublicApi.Support;

/// <summary>
/// The tests of <c>/v1</c> share one server and its template: each run works on a copy of
/// the data set of its own, so no test sees another's requests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class V1Group : ICollectionFixture<V1Server>
{
    public const string Name = "Public API";
}
