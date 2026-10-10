using System.Net;
using System.Text;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Billing.Support;
using LoDb.Api.Tests.Hosting.Support;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// The API reads at most 128 KiB of a request body; the Stripe webhook keeps its own 1 MiB.
/// A larger body is refused with a 413 before it is buffered, which a client error is, not a
/// fault of the server.
/// </summary>
public sealed class RequestBodyLimitTests : IAsyncLifetime
{
    private const int KiB = 1024;
    private const int GlobalLimit = 128 * KiB;
    private const int WebhookLimit = 1024 * KiB;
    private const string LoginPath = "/api/account/login";

    private KestrelApi? _api;

    private KestrelApi Api =>
        _api ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LoginBodyOverTheLimitIsRefusedWith413()
    {
        using var response = await PostAsync(LoginPath, LoginBody(129 * KiB));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task LoginBodyUnderTheLimitIsRead()
    {
        // Cut short: the JSON is read whole, then refused as malformed.
        var body = LoginBody(GlobalLimit)[..(GlobalLimit - 1)];

        using var response = await PostAsync(LoginPath, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WebhookReadsABodyOverTheGlobalLimit()
    {
        using var response = await PostAsync(BillingApp.WebhookPath, Padding(GlobalLimit * 2));

        Assert.Equal(
            "invalid-signature",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task WebhookBodyOverItsOwnLimitIsRefusedWith413()
    {
        using var response = await PostAsync(BillingApp.WebhookPath, Padding(WebhookLimit + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(ApiJson.ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
    }

    public ValueTask InitializeAsync()
    {
        _api = KestrelApi.Start(new Dictionary<string, string?>
        {
            ["LoDb:Billing:StripeSecretKey"] = BillingApp.SecretKey,
            ["LoDb:Billing:StripeWebhookSecret"] = BillingApp.WebhookSecret,
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }
    }

    // A sign-in whose identifier pads the body to exactly `size` bytes.
    private static string LoginBody(int size)
    {
        const string Head = "{\"identifier\":\"";
        const string Tail = "\",\"password\":\"secret\"}";
        return Head + Padding(size - Head.Length - Tail.Length) + Tail;
    }

    private static string Padding(int size) => new('a', size);

    // Headers first, the body only once the server asks for it: a body the server refuses
    // up front is never sent, so the connection is not reset under the answer.
    private async Task<HttpResponseMessage> PostAsync(string path, string body)
    {
        using var client = Api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.ExpectContinue = true;
        return await client.SendAsync(request, Cancellation);
    }
}
