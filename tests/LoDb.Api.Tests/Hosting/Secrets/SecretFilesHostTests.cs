using LoDb.Api.Hosting.Secrets;
using LoDb.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Api.Tests.Hosting.Secrets;

/// <summary>
/// The API host reads its secret settings from the files of its secrets folder: a setting a
/// check requires fails the start as it would if it were missing from the environment, and
/// the start names the settings read, never their values.
/// </summary>
public sealed class SecretFilesHostTests : IDisposable
{
    private const string GoogleClientId = "LoDb:Accounts:Google:ClientId";
    private const string GoogleClientSecret = "LoDb:Accounts:Google:ClientSecret";

    private readonly SecretFolder _folder = new();

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
        const string StripeKey = "LoDb:Billing:StripeSecretKey";
        const string MailPassword = "LoDb:Mail:Password";
        const string Secret = "sk_live_never_logged";
        _folder.With(StripeKey, Secret).With(MailPassword, Secret);
        var logger = new FakeLogger<SecretFilesLog>();
        var secretFiles = new SecretFileSet(
            _folder.Path,
            ["LoDb__Billing__StripeSecretKey", "LoDb__Mail__Password"]);

        await new SecretFilesLog(secretFiles, logger).StartAsync(CancellationToken.None);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(
            ("hosting.secret_files.loaded", LogLevel.Information, "2"),
            (record.Id.Name, record.Level, record.GetStructuredStateValue("Count")));
        var settings = record.GetStructuredStateValue("Settings");
        Assert.Contains(StripeKey, settings, StringComparison.Ordinal);
        Assert.Contains(MailPassword, settings, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, record.Message, StringComparison.Ordinal);
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
