using Microsoft.Extensions.FileProviders;

namespace LoDb.Api.Hosting.Secrets;

/// <summary>
/// Settings read from secret files, above every other source (settings files, environment,
/// arguments): a served host keeps its secrets out of the containers' environment, which
/// <c>docker inspect</c> shows to whoever can query the Docker API.
/// </summary>
/// <remarks>
/// Shared by the web host and the command host, so that <c>migrate</c> reads the database
/// password the way the API does. The files are read once, at start-up: the deploy job
/// recreates the API when one of them changes.
/// </remarks>
internal static class SecretsRegistration
{
    public static IHostApplicationBuilder AddLoDbSecrets(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var secretFiles = SecretFileSet.Read(builder.Configuration);
        AddSecretFiles(builder.Configuration, secretFiles);
        builder.Configuration.AddLoDbDatabasePassword();
        builder.Services.AddSingleton(secretFiles);
        builder.Services.AddHostedService<SecretFilesLog>();
        return builder;
    }

    private static void AddSecretFiles(
        IConfigurationBuilder configuration,
        SecretFileSet secretFiles)
    {
        if (secretFiles.FileNames.Count == 0)
        {
            return;
        }

        configuration.AddKeyPerFile(source =>
        {
            source.FileProvider = new PhysicalFileProvider(secretFiles.Folder);
            source.SectionDelimiter = SecretFileSet.SectionDelimiter;
            source.IgnoreCondition = name => !secretFiles.FileNames.Contains(
                name,
                StringComparer.Ordinal);
        });
    }
}
