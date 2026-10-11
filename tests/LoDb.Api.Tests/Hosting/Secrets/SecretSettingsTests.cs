using LoDb.Api.Hosting.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace LoDb.Api.Tests.Hosting.Secrets;

/// <summary>
/// A secret file wins over every other source of its setting, an empty one leaves the setting
/// to them, and the database password completes the connection string.
/// </summary>
public sealed class SecretSettingsTests : IDisposable
{
    private const string FromFile = "from-file";
    private const string ConnectionStringKey = "ConnectionStrings:LoDb";

    private readonly SecretFolder _folder = new();
    private readonly string _section = "LoDb:SecretSettingsTests:" + Guid.NewGuid().ToString("N");

    [Fact]
    public void FileWinsOverSettingsFilesEnvironmentAndArguments()
    {
        string[] keys = [Key("Settings"), Key("Environment"), Key("Arguments")];
        foreach (var key in keys)
        {
            _folder.With(key, FromFile);
        }

        var configuration = WithEnvironment(Key("Environment"), "from-environment", () =>
            Build(builder => builder.Configuration.AddInMemoryCollection(
                [new(Key("Settings"), "from-settings-file")]),
                $"--{Key("Arguments")}=from-arguments"));

        Assert.All(keys, key => Assert.Equal(FromFile, configuration[key]));
    }

    [Fact]
    public void EmptyFileLeavesTheSettingToTheOtherSources()
    {
        _folder.With(Key("Empty"), string.Empty);

        var configuration = Build(null, $"--{Key("Empty")}=from-arguments");

        Assert.Equal("from-arguments", configuration[Key("Empty")]);
    }

    [Fact]
    public void DatabasePasswordCompletesTheConnectionString()
    {
        const string password = "p@ss;word='x\"";
        _folder.With(DatabasePassword.Key, password);

        var configuration = Build(
            null,
            $"--{ConnectionStringKey}=Host=postgres;Username=lodb;Password=old;Timeout=5");

        var completed = new NpgsqlConnectionStringBuilder(configuration[ConnectionStringKey]);
        Assert.Equal(
            ("postgres", "lodb", password, 5),
            (completed.Host, completed.Username, completed.Password, completed.Timeout));
    }

    [Fact]
    public void WithoutPasswordSettingTheConnectionStringStaysAsConfigured()
    {
        const string configured = "Host=postgres;Username=lodb;Password=kept";

        var configuration = Build(null, $"--{ConnectionStringKey}={configured}");

        Assert.Equal(configured, configuration[ConnectionStringKey]);
    }

    // The data source names the fault when first used, as it does without a password file.
    [Fact]
    public void ConnectionStringThatDoesNotParseStaysAsConfigured()
    {
        const string configured = "not a connection string";
        _folder.With(DatabasePassword.Key, "unused");

        var configuration = Build(null, $"--{ConnectionStringKey}={configured}");

        Assert.Equal(configured, configuration[ConnectionStringKey]);
    }

    public void Dispose() => _folder.Dispose();

    private static T WithEnvironment<T>(
        string key,
        string value,
        Func<T> build)
    {
        var variable = key.Replace(":", "__", StringComparison.Ordinal);
        Environment.SetEnvironmentVariable(variable, value);
        try
        {
            return build();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    private string Key(string name) => ConfigurationPath.Combine(_section, name);

    // The command host's builder: settings files, environment and arguments, as Program has.
    private ConfigurationManager Build(
        Action<HostApplicationBuilder>? arrange,
        params string[] args)
    {
        var builder = Host.CreateApplicationBuilder(
            [$"--{SecretFileSet.FolderKey}={_folder.Path}", .. args]);
        arrange?.Invoke(builder);
        builder.AddLoDbSecrets();
        return builder.Configuration;
    }
}
