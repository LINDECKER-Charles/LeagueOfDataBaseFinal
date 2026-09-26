using System.Globalization;
using System.Net;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class SizeCapTests
{
    private const int Cap = 64;

    private static readonly Dictionary<string, string?> SmallCap = new()
    {
        ["LoDb:Egress:MaxResponseBytes"] = Cap.ToString(CultureInfo.InvariantCulture),
    };

    [Fact]
    public void DefaultCapIs32MiB()
    {
        Assert.Equal(32L * 1024 * 1024, new EgressOptions().MaxResponseBytes);
    }

    [Fact]
    public async Task DeclaredOversizedBodyIsRefusedBeforeReading()
    {
        using var harness = EgressHarness.Create(
            FakeUpstream.Answering(HttpStatusCode.OK, new string('x', Cap + 1)), SmallCap);

        await Assert.ThrowsAsync<EgressBodyTooLargeException>(
            () => harness.FetchAsync(EgressHarness.DdragonUrl));
    }

    // Without a Content-Length the cap bites while streaming: an error, never a truncation.
    [Fact]
    public async Task UndeclaredOversizedBodyFailsWhileReading()
    {
        using var harness = EgressHarness.Create(Unsized(Cap + 1), SmallCap);
        var outcome = await harness.FetchAsync(EgressHarness.DdragonUrl);

        await using var content = Assert.IsType<FetchOutcome.Present>(outcome).Content;

        await Assert.ThrowsAsync<EgressBodyTooLargeException>(
            () => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BodyAtTheCapIsReadWhole()
    {
        using var harness = EgressHarness.Create(Unsized(Cap), SmallCap);
        var outcome = await harness.FetchAsync(EgressHarness.DdragonUrl);

        await using var content = Assert.IsType<FetchOutcome.Present>(outcome).Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, TestContext.Current.CancellationToken);

        Assert.Equal(Cap, copy.Length);
    }

    private static FakeUpstream Unsized(int length) =>
        FakeUpstream.Answering(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnsizedContent(new byte[length]),
        });

    // Chunked transfer: no Content-Length for the fetcher to check up front.
    private sealed class UnsizedContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) => stream.WriteAsync(body).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
