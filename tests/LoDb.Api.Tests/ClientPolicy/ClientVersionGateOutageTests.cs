using System.Net;
using LoDb.Testing;

namespace LoDb.Api.Tests.ClientPolicy;

/// <summary>
/// A policy that cannot be read, the database being down, lets the apps through rather
/// than locking every one of them out on top of the outage.
/// </summary>
public sealed class ClientVersionGateOutageTests
{
    [Fact]
    public async Task AppGoesThroughWhenThePolicyCannotBeRead()
    {
        await using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        using var request = ClientVersionGateTests.Request("/api/no-such-route", "desktop/0.0.1");

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
