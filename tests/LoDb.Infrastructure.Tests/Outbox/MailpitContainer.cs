using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// The Mailpit of the development stack (<c>compose.override.yaml</c>), started for the
/// tests of a class: an SMTP relay that keeps what it receives and shows it through its API.
/// </summary>
public sealed class MailpitContainer : IAsyncLifetime
{
    public const string Image = "axllent/mailpit:v1.31.2";

    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(HttpPort, assignRandomHostPort: true)
        // Plain SMTP with any login, as in development.
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(static request =>
            request.ForPort(HttpPort).ForPath("/readyz")))
        .Build();

    private HttpClient? _api;

    /// <summary>The settings that point the outbox at this relay.</summary>
    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["LoDb:Mail:Host"] = _container.Hostname,
        ["LoDb:Mail:Port"] = _container.GetMappedPublicPort(SmtpPort).ToString(
            System.Globalization.CultureInfo.InvariantCulture),
        ["LoDb:Mail:Security"] = "None",
    };

    private HttpClient Api => _api ??= new HttpClient
    {
        BaseAddress = new UriBuilder(
            Uri.UriSchemeHttp,
            _container.Hostname,
            _container.GetMappedPublicPort(HttpPort)).Uri,
    };

    /// <summary>The full message received for <paramref name="recipient"/>, if one was.</summary>
    public async Task<JsonElement?> MessageToAsync(
        string recipient,
        CancellationToken cancellationToken)
    {
        var list = await Api.GetFromJsonAsync<JsonElement>(
            "/api/v1/messages",
            cancellationToken);
        var summary = list.GetProperty("messages").EnumerateArray().FirstOrDefault(message =>
            message.GetProperty("To").EnumerateArray().Any(to =>
                to.GetProperty("Address").GetString() == recipient));
        if (summary.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var id = summary.GetProperty("ID").GetString();
        return await Api.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}", cancellationToken);
    }

    public async ValueTask InitializeAsync() =>
        await _container.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        _api?.Dispose();
        await _container.DisposeAsync();
    }
}
