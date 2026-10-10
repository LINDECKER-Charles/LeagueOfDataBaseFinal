namespace LoDb.Api.Hosting.Secrets;

/// <summary>
/// Names, once the web host starts, the settings read from secret files: after a deployment,
/// the log shows which secrets the API received, never their values.
/// </summary>
internal sealed partial class SecretFilesLog(
    SecretFileSet secretFiles,
    ILogger<SecretFilesLog> logger) : IHostedService
{
    private const string Separator = ", ";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (secretFiles.FileNames.Count > 0 && logger.IsEnabled(LogLevel.Information))
        {
            var settings = string.Join(Separator, secretFiles.SettingKeys);
            LogLoaded(logger, secretFiles.FileNames.Count, settings);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventName = "hosting.secret_files.loaded",
        Level = LogLevel.Information,
        Message = "{Count} settings read from secret files: {Settings}.")]
    private static partial void LogLoaded(ILogger logger, int count, string settings);
}
