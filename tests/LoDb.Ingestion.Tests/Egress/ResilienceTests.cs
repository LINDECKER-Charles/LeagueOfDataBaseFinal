using System.Net;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class ResilienceTests
{
    private const int Attempts = 4;

    private static readonly Dictionary<string, string?> ShortTimeout = new()
    {
        ["LoDb:Egress:AttemptTimeout"] = "00:00:00.050",
        ["LoDb:Egress:MaxRetryAttempts"] = "1",
    };

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task PersistentFailureIsRetriedThenTransient(HttpStatusCode status)
    {
        var upstream = FakeUpstream.Answering(status);
        using var harness = EgressHarness.Create(upstream);

        var failure = await Assert.ThrowsAsync<EgressTransientException>(
            () => harness.FetchAsync(EgressHarness.DdragonUrl));

        Assert.Equal(status, failure.StatusCode);
        Assert.Equal(Attempts, upstream.Calls);
    }

    [Fact]
    public async Task PassingFailureIsRetriedIntoSuccess()
    {
        var upstream = FakeUpstream.Sequence(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK);
        using var harness = EgressHarness.Create(upstream);

        var outcome = await harness.FetchAsync(EgressHarness.DdragonUrl);

        await using var content = Assert.IsType<FetchOutcome.Present>(outcome).Content;
        Assert.Equal(3, upstream.Calls);
    }

    [Fact]
    public async Task HangingUpstreamTimesOutPerAttemptThenIsTransient()
    {
        var upstream = new FakeUpstream(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return FakeUpstream.Response(HttpStatusCode.OK, "late");
        });
        using var harness = EgressHarness.Create(upstream, ShortTimeout);

        await Assert.ThrowsAsync<EgressTransientException>(
            () => harness.FetchAsync(EgressHarness.DdragonUrl));

        Assert.Equal(2, upstream.Calls);
    }

    [Fact]
    public async Task TransportFailureIsRetriedThenTransient()
    {
        var upstream = new FakeUpstream(
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("reset")));
        using var harness = EgressHarness.Create(upstream);

        var failure = await Assert.ThrowsAsync<EgressTransientException>(
            () => harness.FetchAsync(EgressHarness.DdragonUrl));

        Assert.IsType<HttpRequestException>(failure.InnerException);
        Assert.Equal(Attempts, upstream.Calls);
    }

    // The deadline also covers the body, read after the pipeline has returned.
    [Fact]
    public async Task StalledBodyTimesOutAsTransient()
    {
        var upstream = FakeUpstream.Answering(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new StalledStream()),
        });
        using var harness = EgressHarness.Create(upstream, ShortTimeout);
        var outcome = await harness.FetchAsync(EgressHarness.DdragonUrl);

        await using var content = Assert.IsType<FetchOutcome.Present>(outcome).Content;

        await Assert.ThrowsAsync<EgressTransientException>(
            () => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CallerCancellationIsNeitherRetriedNorTransient()
    {
        using var cancellation = new CancellationTokenSource();
        var upstream = new FakeUpstream(async (_, token) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
            return FakeUpstream.Response(HttpStatusCode.OK, "late");
        });
        using var harness = EgressHarness.Create(upstream);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Fetcher.FetchAsync(
                new Uri(EgressHarness.DdragonUrl), cancellation.Token));

        Assert.Equal(1, upstream.Calls);
    }

    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override bool CanSeek => false;
    }
}
