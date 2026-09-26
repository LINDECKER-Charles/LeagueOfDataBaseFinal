using LoDb.Api.Hosting.Health;
using LoDb.Api.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Testing;

/// <summary>
/// The API host in memory, cut off from the machine: background services off, a private
/// storage root, and a database that answers only when a test supplies one.
/// </summary>
/// <remarks>
/// Settings go through <c>UseSetting</c>: the test host hands them to <c>Program</c> as
/// arguments, so the modules see them while they register their services, which is not the
/// case for configuration sources added once the host is built.
/// </remarks>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>A port nothing listens on: readiness fails at once instead of timing out.</summary>
    public const string UnreachablePostgres = "Host=127.0.0.1;Port=1;Timeout=2;Pooling=false";

    private const string StorageDirectoryPrefix = "lodb-tests-";
    private const string QuietLogLevelKey = "Logging:LogLevel:Default";
    private const string QuietLogLevel = "Warning";

    private readonly Lazy<DirectoryInfo> _storageRoot =
        new(() => Directory.CreateTempSubdirectory(StorageDirectoryPrefix));

    /// <summary><c>ConnectionStrings:LoDb</c>, unreachable unless a test sets it.</summary>
    public string PostgresConnectionString { get; init; } = UnreachablePostgres;

    /// <summary>Extra settings, applied last so that they win over the defaults above.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Clock of the host; the real one when unset.</summary>
    public FakeTimeProvider? Clock { get; init; }

    /// <summary>Fresh <c>LoDb:Storage:Root</c> directory, deleted with the factory.</summary>
    public string StorageRoot => _storageRoot.Value.FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(ConventionalWorkers.EnabledKey, bool.FalseString);
        builder.UseSetting(
            $"ConnectionStrings:{PostgresReadinessCheck.ConnectionStringName}",
            PostgresConnectionString);
        builder.UseSetting(StorageReadinessCheck.RootKey, StorageRoot);
        builder.UseSetting(QuietLogLevelKey, QuietLogLevel);
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IStartupFilter, LocalPortFromHostHeader>();
            if (Clock is not null)
            {
                services.AddSingleton<TimeProvider>(Clock);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _storageRoot.IsValueCreated && _storageRoot.Value.Exists)
        {
            _storageRoot.Value.Delete(recursive: true);
        }
    }
}
