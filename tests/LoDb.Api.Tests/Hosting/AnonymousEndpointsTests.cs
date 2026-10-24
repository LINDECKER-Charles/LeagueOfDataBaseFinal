using LoDb.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// No endpoint skips authorization, and with it the forgery guard every policy carries, save
/// the two analytics captures due to leave with the in-house analytics and nginx's reports to
/// the robots journal (ADR 0013), which only the stack's network reaches.
/// </summary>
public sealed class AnonymousEndpointsTests
{
    private static readonly string[] Captures =
    [
        "/api/analytics/view",
        "/internal/analytics/page",
        "/internal/robots/journal",
    ];

    [Fact]
    public async Task OnlyTheInternalCapturesAllowAnonymous()
    {
        await using var factory = new ApiFactory();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var anonymous = endpoints
            .Where(static endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(static endpoint => (endpoint as RouteEndpoint)?.RoutePattern.RawText)
            .Order(StringComparer.Ordinal);

        Assert.Equal(Captures, anonymous);
    }
}
