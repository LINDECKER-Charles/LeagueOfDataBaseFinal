using LoDb.Api.Modules.Admin.Scaleway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Scaleway;

/// <summary>
/// The <c>scaleway-billing</c> client as the API registers it, its primary handler kept:
/// toward Scaleway's API alone, a short timeout, a capped answer, no redirect followed and no
/// log line per request, whose headers hold the key.
/// </summary>
public sealed class ScalewayBillClientTests : IDisposable
{
    // A client registered without a word: the factory's defaults, loggers included.
    private const string DefaultClient = "default-of-the-factory";
    private const string LoggingNamespace = "Microsoft.Extensions.Http.Logging";

    private readonly ServiceProvider _services;

    public ScalewayBillClientTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddAdminScalewayBilling(new ConfigurationBuilder().Build());
        services.AddHttpClient(DefaultClient);
        _services = services.BuildServiceProvider();
    }

    [Fact]
    public void ClientReachesScalewaysApiAloneWithinTenSecondsAndAMegabyte()
    {
        var client = _services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(ScalewayBillingClient.ClientName);

        Assert.Equal(new Uri("https://api.scaleway.com"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
        Assert.Equal(1024 * 1024, client.MaxResponseContentBufferSize);
    }

    [Fact]
    public void HandlerFollowsNoRedirectKeepsNoCookieAndLogsNoRequest()
    {
        var chain = Chain(ScalewayBillingClient.ClientName);

        var primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.False(primary.AllowAutoRedirect);
        Assert.False(primary.UseCookies);
        Assert.DoesNotContain(chain, static handler => IsLogging(handler));
        // The factory's defaults log each request: the client removed them.
        Assert.Contains(Chain(DefaultClient), static handler => IsLogging(handler));
    }

    public void Dispose() => _services.Dispose();

    private List<HttpMessageHandler> Chain(string name)
    {
        var chain = new List<HttpMessageHandler>();
        HttpMessageHandler? handler = _services.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(name);
        while (handler is not null)
        {
            chain.Add(handler);
            handler = (handler as DelegatingHandler)?.InnerHandler;
        }

        return chain;
    }

    private static bool IsLogging(HttpMessageHandler handler) =>
        handler.GetType().Namespace == LoggingNamespace;
}
