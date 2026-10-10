using LoDb.Api.Hosting.Secrets;
using LoDb.Api.Modules.Billing;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Hosting.Secrets;

/// <summary>
/// The API host reads its secret settings from the files of its secrets folder: the options
/// see the file over any other source, and a setting a check requires fails the start as it
/// would if it were missing from the environment.
/// </summary>
public sealed class SecretFilesHostTests : IDisposable
{
    private const string WebhookSecret = BillingOptions.SectionName + ":StripeWebhookSecret";
    private const string GoogleClientId = "LoDb:Accounts:Google:ClientId";
    private const string GoogleClientSecret = "LoDb:Accounts:Google:ClientSecret";

    private readonly SecretFolder _folder = new();

    [Fact]
    public async Task OptionsReadTheFileOverTheSetting()
    {
        _folder.With(WebhookSecret, "whsec_from_file");
        await using var factory = Factory(new() { [WebhookSecret] = "whsec_from_setting" });

        var billing = factory.Services.GetRequiredService<IOptions<BillingOptions>>().Value;

        Assert.Equal("whsec_from_file", billing.StripeWebhookSecret);
    }

    [Fact]
    public async Task RequiredSecretLeftEmptyStopsTheHostAtStart()
    {
        _folder.With(GoogleClientSecret, string.Empty);
        await using var factory = Factory(new() { [GoogleClientId] = "client.example.test" });

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains(
            "Google:ClientSecret is required with Google:ClientId.",
            Flatten(exception),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequiredSecretInItsFileLetsTheHostStart()
    {
        _folder.With(GoogleClientSecret, "google-secret");
        await using var factory = Factory(new() { [GoogleClientId] = "client.example.test" });

        Assert.NotNull(factory.Server);
    }

    [Fact]
    public async Task StartNamesTheSettingsReadFromFilesWithoutTheirValues()
    {
        var logger = new FakeLogger<SecretFilesLog>();
        var secretFiles = new SecretFileSet(
            _folder.Path,
            ["LoDb__Billing__StripeSecretKey", "LoDb__Mail__Password"]);

        await new SecretFilesLog(secretFiles, logger).StartAsync(CancellationToken.None);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal("hosting.secret_files.loaded", record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(
            "2 settings read from secret files: LoDb:Billing:StripeSecretKey, LoDb:Mail:Password.",
            record.Message);
    }

    [Fact]
    public async Task StartSaysNothingWithoutSecretFiles()
    {
        var logger = new FakeLogger<SecretFilesLog>();

        await new SecretFilesLog(new SecretFileSet(_folder.Path, []), logger)
            .StartAsync(CancellationToken.None);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    public void Dispose() => _folder.Dispose();

    private static string Flatten(Exception exception) =>
        exception is AggregateException aggregate
            ? string.Join(' ', aggregate.InnerExceptions.Select(Flatten))
            : exception.Message + ' ' + (exception.InnerException is { } inner
                ? Flatten(inner)
                : string.Empty);

    private ApiFactory Factory(Dictionary<string, string?> settings)
    {
        settings[SecretFileSet.FolderKey] = _folder.Path;
        return new ApiFactory { Settings = settings };
    }
}
