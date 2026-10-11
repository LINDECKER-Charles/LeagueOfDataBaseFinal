using LoDb.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// Each costly action of a signed-in account carries its hourly limit by account: a Stripe
/// checkout, an API key issued, a build saved, a vote.
/// </summary>
public sealed class AccountActionLimitCoverageTests
{
    [Theory]
    [InlineData("openPackCheckout", "purchase-checkout")]
    [InlineData("openPlanCheckout", "purchase-checkout")]
    [InlineData("createApiKey", "api-key-issue")]
    [InlineData("regenerateApiKey", "api-key-issue")]
    [InlineData("createBuild", "build-edit")]
    [InlineData("updateBuild", "build-edit")]
    [InlineData("voteBuild", "build-vote")]
    public async Task CostlyActionRequiresItsLimit(string operation, string policy)
    {
        await using var factory = new ApiFactory();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var endpoint = Assert.Single(
            endpoints,
            endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                == operation);

        Assert.Equal(
            policy,
            endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }
}
