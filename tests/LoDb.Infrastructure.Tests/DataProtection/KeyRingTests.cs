using System.Security.Cryptography;
using LoDb.Infrastructure.DataProtection;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Infrastructure.Tests.DataProtection;

/// <summary>
/// The key ring lives in <c>data_protection_keys</c>, shared by every instance: encrypted by
/// the certificate when one is set, in clear with a warning in development, and never
/// created in clear anywhere else.
/// </summary>
public sealed class KeyRingTests(PostgresContainerFixture postgres) : MigratedDatabase(postgres)
{
    private const string Purpose = "LoDb.Tests.KeyRing";
    private const string KeysQuery = "SELECT xml FROM data_protection_keys";

    private static readonly byte[] Payload = "sign-in cookie"u8.ToArray();
    private static readonly Dictionary<string, string?> NoCertificate = [];

    private readonly List<ServiceProvider> _instances = [];

    [Fact]
    public async Task KeysAreEncryptedByTheCertificateAndSharedByInstances()
    {
        using var certificate = TemporaryCertificate.Create(withPrivateKey: true);
        var first = Instance(Environments.Production, certificate.Settings());
        var second = Instance(Environments.Production, certificate.Settings());

        var protectedPayload = Protector(first).Protect(Payload);

        Assert.Equal(Payload, Protector(second).Unprotect(protectedPayload));
        var xml = Assert.Single(await Database.QueryAsync(KeysQuery, Cancellation));
        Assert.Contains("encryptedSecret", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("masterKey", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DevelopmentStoresKeysInClearWithAWarning()
    {
        var instance = Instance(Environments.Development, NoCertificate);

        Protector(instance).Protect(Payload);

        var xml = Assert.Single(await Database.QueryAsync(KeysQuery, Cancellation));
        Assert.Contains("masterKey", xml, StringComparison.Ordinal);
        Assert.Contains(
            Logs(instance),
            static log => log.Id.Name == "dataprotection.keys.unencrypted"
                && log.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task OtherEnvironmentsCreateNoKeyWithoutCertificate()
    {
        var instance = Instance(Environments.Production, NoCertificate);

        var error = Assert.Throws<CryptographicException>(
            () => Protector(instance).Protect(Payload));

        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Empty(await Database.QueryAsync(KeysQuery, Cancellation));
        Assert.Contains(
            Logs(instance),
            static log => log.Id.Name == "dataprotection.certificate.missing"
                && log.Level == LogLevel.Critical);
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        foreach (var instance in _instances)
        {
            await instance.DisposeAsync();
        }
    }

    private static IDataProtector Protector(ServiceProvider instance) =>
        instance.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

    private static IReadOnlyList<FakeLogRecord> Logs(ServiceProvider instance) =>
        instance.GetFakeLogCollector().GetSnapshot();

    // One instance of the API: its own services and pool, the database of the test.
    private ServiceProvider Instance(string environment, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{LoDbDataSource.ConnectionStringName}"] =
                    Database.ConnectionString,
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IHostEnvironment>(new HostingEnvironment
            {
                EnvironmentName = environment,
            })
            .AddSingleton(TimeProvider.System)
            .AddFakeLogging()
            .AddLoDbPersistence(configuration)
            .AddLoDbDataProtection(configuration);
        var instance = services.BuildServiceProvider(validateScopes: true);
        _instances.Add(instance);
        return instance;
    }
}
