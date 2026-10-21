using LoDb.Infrastructure.PageStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Infrastructure.Tests.PageStore.Support;

/// <summary>
/// The page store as the API registers it, on the given settings, with every log line kept
/// (trace level included); its client's wire replaced by <c>primary</c> when one is given.
/// </summary>
internal sealed class PageStoreHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public PageStoreHarness(
        IReadOnlyDictionary<string, string?> settings,
        HttpMessageHandler? primary = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection()
            .AddLogging(static logging => logging
                .SetMinimumLevel(LogLevel.Trace)
                .AddFakeLogging())
            .AddLoDbPageStore(configuration);
        if (primary is not null)
        {
            services.AddHttpClient(PageStoreRegistration.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => primary);
        }

        _provider = services.BuildServiceProvider();
    }

    public IPageStore Store => _provider.GetRequiredService<IPageStore>();

    /// <summary>Every line logged so far, by any category.</summary>
    public IReadOnlyList<FakeLogRecord> Logs => _provider.GetFakeLogCollector().GetSnapshot();

    public HttpClient Client() => _provider.GetRequiredService<IHttpClientFactory>()
        .CreateClient(PageStoreRegistration.ClientName);

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
