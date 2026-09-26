using LoDb.Infrastructure.Analytics;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.DataProtection;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests;

/// <summary>
/// The zone registrations called by Program.cs accept an empty configuration: the OpenAPI
/// generation builds the host with nothing set, so they must neither read a required value
/// nor throw.
/// </summary>
public sealed class ZoneRegistrationTests
{
    private static readonly IConfiguration Empty = new ConfigurationBuilder().Build();

    public static TheoryData<string> Zones =>
        ["Storage", "Persistence", "Jobs", "Outbox", "Audit", "DataProtection", "Analytics"];

    [Theory]
    [MemberData(nameof(Zones))]
    public void ZoneRegistersWithAnEmptyConfiguration(string zone)
    {
        var services = new ServiceCollection();

        var returned = Register(zone, services);

        Assert.Same(services, returned);
    }

    private static IServiceCollection Register(string zone, IServiceCollection services) =>
        zone switch
        {
            "Storage" => services.AddLoDbStorage(Empty),
            "Persistence" => services.AddLoDbPersistence(Empty),
            "Jobs" => services.AddLoDbJobs(Empty),
            "Outbox" => services.AddLoDbOutbox(Empty),
            "Audit" => services.AddLoDbAudit(Empty),
            "DataProtection" => services.AddLoDbDataProtection(Empty),
            "Analytics" => services.AddLoDbAnalytics(Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, null),
        };
}
