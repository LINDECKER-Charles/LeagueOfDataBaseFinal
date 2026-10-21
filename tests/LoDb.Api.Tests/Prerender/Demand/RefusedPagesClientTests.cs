using System.Net;
using System.Text;
using LoDb.Api.Modules.Admin.Prerender.Demand;
using LoDb.Api.Modules.Admin.Prerender.Demand.Capture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// The SSR's <c>/refused</c> read as the queue reads it: its JSON from the cursor asked for,
/// and nothing (never an exception) for a failure or an answer that is not the report.
/// </summary>
public sealed class RefusedPagesClientTests
{
    private const string Report = """
        {"instance":"5d0f6c1e-0d5e-4c55-9b55-37a4c0e7c7a1","cursor":42,"isComplete":true,
         "evicted":3,"pages":[{"path":"/fr/15.13.1/champions/Aatrox","count":7}]}
        """;

    private static readonly Uri Url = new("http://lodb-web-ssr:9464/refused");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReportIsReadFromTheCursorAskedFor()
    {
        Uri? asked = null;

        var report = await ReadAsync(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, Report);
        });

        Assert.Equal("http://lodb-web-ssr:9464/refused?after=17", asked?.ToString());
        Assert.NotNull(report);
        Assert.Equal(
            ("5d0f6c1e-0d5e-4c55-9b55-37a4c0e7c7a1", 42L, true, 3L),
            (report.Instance, report.Cursor, report.IsComplete, report.Evicted));
        Assert.Equal([new RefusedPageCount("/fr/15.13.1/champions/Aatrox", 7)], report.Pages);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, Report)]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"cursor":1}""")]
    public async Task AnswerThatIsNotTheReportReadsAsNothing(HttpStatusCode status, string body)
    {
        Assert.Null(await ReadAsync(_ => Answer(status, body)));
    }

    [Fact]
    public async Task UnreachableSsrReadsAsNothing()
    {
        Assert.Null(await ReadAsync(static _ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("refused"))));
    }

    private static Task<HttpResponseMessage> Answer(HttpStatusCode status, string body) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    private static async Task<RefusedPagesReport?> ReadAsync(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AddPrerenderDemand(configuration);
        services.AddHttpClient(RefusedPagesClient.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(answer));
        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<IRefusedPagesReader>()
            .ReadAsync(Url, 17, Token);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => answer(request);
    }
}
