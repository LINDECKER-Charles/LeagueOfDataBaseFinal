using System.Net;
using System.Text;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// An SSR's <c>/load</c> read as the regulator reads it: its JSON, and nothing (never an
/// exception) for a failure, an answer too slow or too large, or one that is not the load.
/// </summary>
public sealed class SsrLoadClientTests
{
    private const string Load = """
        {"cpuSeconds":12.5,"eventLoopDelayP99Seconds":0.013,"cpuPressureSomeAvg10":0.021,
         "cpuThrottledSeconds":null,
         "lanes":{"current":{"renders":40,"renderSeconds":6.5,"refused":1},
                  "pinned":{"renders":2,"renderSeconds":0.5,"refused":0},
                  "prerender":{"renders":0,"renderSeconds":0,"refused":0}}}
        """;

    private static readonly Uri Url = new("http://lodb-web-ssr:9464/load");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LoadIsReadFromItsJson()
    {
        var load = await ReadAsync(_ => Answer(HttpStatusCode.OK, Load));

        Assert.NotNull(load);
        Assert.Equal(
            (12.5, 0.013, 0.021, (double?)null),
            (load.CpuSeconds, load.EventLoopDelayP99Seconds, load.CpuPressureSomeAvg10,
                load.CpuThrottledSeconds));
        Assert.Equal(
            (40L, 6.5, 1L),
            (load.Lanes.Current.Renders, load.Lanes.Current.RenderSeconds,
                load.Lanes.Current.Refused));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, Load)]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"cpuSeconds":1}""")]
    public async Task AnswerThatIsNotTheLoadReadsAsNothing(HttpStatusCode status, string body)
    {
        Assert.Null(await ReadAsync(_ => Answer(status, body)));
    }

    [Fact]
    public async Task OversizedAnswerReadsAsNothing()
    {
        var padding = new string(' ', 65 * 1024);

        Assert.Null(await ReadAsync(_ => Answer(HttpStatusCode.OK, Load + padding)));
    }

    [Fact]
    public async Task UnreachableOrSlowSsrReadsAsNothing()
    {
        var unreachable = await ReadAsync(static _ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("refused")));
        var slow = await ReadAsync(static async cancellation =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellation);
            return await Answer(HttpStatusCode.OK, Load);
        });

        Assert.Null(unreachable);
        Assert.Null(slow);
    }

    private static Task<HttpResponseMessage> Answer(HttpStatusCode status, string body) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    private static async Task<SsrLoad?> ReadAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> answer)
    {
        var services = new ServiceCollection().AddPrerenderPace();
        services.AddHttpClient(SsrLoadClient.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(answer));
        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<ISsrLoadReader>().ReadAsync(Url, Token);
    }

    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => answer(cancellationToken);
    }
}
