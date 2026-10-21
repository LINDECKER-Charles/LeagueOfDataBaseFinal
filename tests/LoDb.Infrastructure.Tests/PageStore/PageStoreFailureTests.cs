using System.IO.Pipelines;
using System.Net;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Infrastructure.Tests.PageStore.Support;

namespace LoDb.Infrastructure.Tests.PageStore;

/// <summary>
/// What the store cannot be made to do on demand, through a fake wire: its outages are typed
/// transient failures, a store that refuses the writer is a configuration error, a caller's
/// cancellation stays one, and the client sends nothing that leaves the configured bucket or
/// exceeds the metadata limit.
/// </summary>
public sealed class PageStoreFailureTests
{
    private const string Key = "pages/1/fr/16.1.1/items/1001-bottes";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Server errors, Scaleway's throttling, a request timeout, a concurrent write to the key.
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task AnOutageIsTransient(HttpStatusCode status)
    {
        var store = new FakeStore((_, _) => Task.FromResult(Answer(status, "SlowDown")));
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);

        var failure = await Assert.ThrowsAsync<PageStoreTransientException>(
            () => harness.Store.PutAsync(Page(), Cancellation));

        Assert.Equal(status, failure.StatusCode);
        Assert.Contains("SlowDown", failure.Message, StringComparison.Ordinal);
    }

    // A rotated key, a policy or an address that no longer lets the writer in, a missing
    // bucket, a wrong endpoint, a store that cannot honour a conditional write.
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.NotImplemented)]
    [InlineData(HttpStatusCode.HttpVersionNotSupported)]
    public async Task AStoreThatRefusesTheWriterIsAConfigurationError(HttpStatusCode status)
    {
        var store = new FakeStore((_, _) => Task.FromResult(Answer(status, "AccessDenied")));
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);

        var failure = await Assert.ThrowsAsync<PageStoreConfigurationException>(
            () => harness.Store.PutAsync(Page(), Cancellation));

        Assert.Equal(status, failure.StatusCode);
    }

    // A malformed request, a precondition on a write that is not create-only: this object.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.PreconditionFailed)]
    public async Task AnyOtherClientErrorRefusesTheObjectAlone(HttpStatusCode status)
    {
        var store = new FakeStore((_, _) => Task.FromResult(Answer(status, "InvalidArgument")));
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);

        var failure = await Assert.ThrowsAsync<PageStoreRefusedException>(
            () => harness.Store.PutAsync(Page(), Cancellation));

        Assert.Equal(status, failure.StatusCode);
    }

    // The connection drops while the error's body is read.
    [Fact]
    public async Task ABodyCutShortIsTransient()
    {
        var store = new FakeStore(static (_, _) =>
        {
            var pipe = new Pipe();
            pipe.Writer.Complete(new IOException("Connection reset by peer."));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StreamContent(pipe.Reader.AsStream()),
            });
        });
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);

        await Assert.ThrowsAsync<PageStoreTransientException>(
            () => harness.Store.PutAsync(Page(), Cancellation));
    }

    [Fact]
    public async Task ATimeoutIsTransient()
    {
        var store = new FakeStore(static async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var harness = new PageStoreHarness(
            FakeStore.Settings(("RequestTimeout", "00:00:00.200")),
            store);

        await Assert.ThrowsAsync<PageStoreTransientException>(
            () => harness.Store.HeadAsync(Key, Cancellation));
    }

    [Fact]
    public async Task ATransportFailureIsTransient()
    {
        var store = new FakeStore(static (_, _) =>
            throw new HttpRequestException("Connection refused."));
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);

        await Assert.ThrowsAsync<PageStoreTransientException>(
            () => harness.Store.PutAsync(Page(), Cancellation));
    }

    [Fact]
    public async Task ACancellationByTheCallerIsNotAFailureOfTheStore()
    {
        using var caller = new CancellationTokenSource();
        var store = new FakeStore(async (_, cancellationToken) =>
        {
            await caller.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Store.PutAsync(Page(), caller.Token));
    }

    // Another host, the endpoint over http, another bucket, a query.
    [Theory]
    [InlineData("https://elsewhere.example.test/lodb-pages-test/pages/1/x")]
    [InlineData("http://s3.fr-par.scw.cloud/lodb-pages-test/pages/1/x")]
    [InlineData("https://s3.fr-par.scw.cloud/lodb-pages-other/pages/1/x")]
    [InlineData("https://s3.fr-par.scw.cloud/lodb-pages-test/?list-type=2")]
    public async Task TheClientRefusesAnyOtherDestination(string url)
    {
        var store = new FakeStore((_, _) => Task.FromResult(Answer(HttpStatusCode.OK)));
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);
        using var client = harness.Client();

        await Assert.ThrowsAsync<PageStoreConfigurationException>(
            () => client.GetAsync(new Uri(url), Cancellation));

        Assert.Empty(store.Requests);
    }

    // 2 KB of prefixed names and values in all is the limit: one byte more is refused before
    // sending.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task MetadataOverTheLimitAreRefusedBeforeSending(int excess, bool isSent)
    {
        var store = new FakeStore((_, _) => Task.FromResult(Answer(HttpStatusCode.OK)));
        await using var harness = new PageStoreHarness(FakeStore.Settings(), store);
        const string Name = "content-security-policy";
        const string PrefixedName = $"x-amz-meta-{Name}";
        var value = new string('a', (2 * 1024) - PrefixedName.Length + excess);
        var page = Page() with { Metadata = new Dictionary<string, string> { [Name] = value } };

        var write = harness.Store.PutAsync(page, Cancellation);

        if (isSent)
        {
            Assert.Equal(PageWriteOutcome.Written, await write);
        }
        else
        {
            await Assert.ThrowsAsync<PageStoreRefusedException>(() => write);
        }

        Assert.Equal(isSent ? 1 : 0, store.Requests.Count);
    }

    [Fact]
    public async Task AnUnconfiguredStoreIsNeverReached()
    {
        var store = new FakeStore((_, _) => Task.FromResult(Answer(HttpStatusCode.OK)));
        await using var harness = new PageStoreHarness(
            FakeStore.Settings(("SecretKey", null)),
            store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Store.PutAsync(Page(), Cancellation));

        Assert.Empty(store.Requests);
    }

    private static PageObject Page() => StoredPages.Page(Key, new Dictionary<string, string>());

    private static HttpResponseMessage Answer(HttpStatusCode status, string? code = null) =>
        new(status)
        {
            Content = new StringContent(code is null
                ? string.Empty
                : $"<?xml version=\"1.0\"?><Error><Code>{code}</Code></Error>"),
        };
}
