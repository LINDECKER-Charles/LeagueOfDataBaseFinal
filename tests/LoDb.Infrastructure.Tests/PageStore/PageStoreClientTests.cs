using System.Net;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.PageStore.Errors;
using LoDb.Infrastructure.Tests.PageStore.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace LoDb.Infrastructure.Tests.PageStore;

/// <summary>
/// The <c>page-store</c> client on the wire it is registered with: a redirect is never
/// followed (a signed request goes to the endpoint alone), a cookie never comes back, and no
/// compression is asked for (a page is stored gzip as the SSR made it).
/// </summary>
public sealed class PageStoreClientTests
{
    private const string Key = "pages/1/fr/16.1.1/items/1001-bottes";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheClientFollowsNoRedirectKeepsNoCookieAndAsksNoCompression()
    {
        await using var server = await LoopbackStore.StartAsync(Answer, Cancellation);
        await using var harness = new PageStoreHarness(FakeStore.Settings(
            (nameof(PageStoreOptions.Endpoint), server.Endpoint),
            (nameof(PageStoreOptions.AllowInsecureEndpoint), bool.TrueString)));

        var page = StoredPages.Page(Key, new Dictionary<string, string>());

        var redirect = await Assert.ThrowsAsync<PageStoreConfigurationException>(
            () => harness.Store.PutAsync(page, Cancellation));
        var stored = await harness.Store.HeadAsync(Key, Cancellation);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
        Assert.Null(stored);
        Assert.Equal(2, server.Requests.Count);
        Assert.DoesNotContain(server.Requests, static headers =>
            headers.ContainsKey("cookie") || headers.ContainsKey("accept-encoding"));

        // The write is sent elsewhere in the bucket with a cookie; any other request is unknown.
        static void Answer(HttpContext context)
        {
            if (!HttpMethods.IsPut(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            context.Response.Headers[HeaderNames.Location] = $"/{FakeStore.Bucket}/elsewhere";
            context.Response.Headers[HeaderNames.SetCookie] = "tracker=1; Path=/";
        }
    }
}
