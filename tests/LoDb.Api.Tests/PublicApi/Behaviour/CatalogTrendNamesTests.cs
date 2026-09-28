using LoDb.Api.Modules.PublicApi.Trends;
using LoDb.Api.Modules.PublicApi.Trends.Reading;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.PublicApi.Behaviour;

/// <summary>
/// The rankings of <c>/v1/trends</c> are named from the latest catalog in en_US, keyed as
/// go-api keyed them; before any ingestion they go unnamed.
/// </summary>
public sealed class CatalogTrendNamesTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheLatestCatalogNamesEveryType()
    {
        await using var database = await postgres.CreateDatabaseAsync(Token);
        await database.MigrateAsync(Token);
        await using var factory = new ApiFactory
        {
            PostgresConnectionString = database.ConnectionString,
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
        var names = host.Services.GetRequiredService<ITrendNames>();
        var unnamed = await names.NamesAsync(TrendType.Champions, Token);

        var ingested = await host.Services.GetRequiredService<IVersionIngestion>()
            .IngestAsync(DdragonFixtures.Latest, IngestionRequest.Complete, Token);
        var champions = await names.NamesAsync(TrendType.Champions, Token);
        var items = await names.NamesAsync(TrendType.Items, Token);
        var runes = await names.NamesAsync(TrendType.Runes, Token);
        var summoners = await names.NamesAsync(TrendType.Summoners, Token);

        Assert.Empty(unnamed);
        Assert.Equal(VersionIngestionOutcome.Completed, ingested.Outcome);
        Assert.Equal("Ahri", champions["Ahri"]);
        Assert.Equal("Berserker's Greaves", items["3006"]);
        Assert.Equal("Flash", summoners["SummonerFlash"]);
        Assert.Equal("Domination", runes["8100"]);
        Assert.Equal("Domination", runes["Domination"]);
        Assert.Equal("Electrocute", runes["8112"]);
        Assert.Equal("Electrocute", runes["Electrocute"]);
    }
}
