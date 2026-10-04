using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Tests;

/// <summary>
/// The zone registrations called by Program.cs accept an empty configuration: the OpenAPI
/// generation builds the host with nothing set, so they must neither read a required value
/// nor throw.
/// </summary>
public sealed class ZoneRegistrationTests
{
    private static readonly IConfiguration Empty = new ConfigurationBuilder().Build();

    public static TheoryData<string> Zones => ["Egress", "Ddragon", "Ingestion", "Catalog"];

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
            "Egress" => services.AddLoDbEgress(Empty),
            "Ddragon" => services.AddLoDbDdragon(Empty),
            "Ingestion" => services.AddLoDbIngestion(Empty),
            "Catalog" => services.AddLoDbCatalog(Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, null),
        };
}
