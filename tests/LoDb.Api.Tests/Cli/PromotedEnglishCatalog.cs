using LoDb.Api.Cli;
using LoDb.Api.Tests.Ingestion;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// The latest recorded version as the index of states reads it: its en_US datasets stored by
/// the <c>ingest</c> command, then the version promoted.
/// </summary>
internal static class PromotedEnglishCatalog
{
    public static async Task StoreAsync(IngestionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var replay = DdragonFixtures.CreateReplay();
        var latest = DdragonFixtures.Latest.Value;
        var ingested = await CliRunner.RunAsync(
            ["ingest", .. host.Settings, "--version", latest, "--languages", "en_US"],
            typeof(Program).Assembly,
            (services, configuration) => IngestionHost.AddZones(services, configuration, replay));
        Assert.Equal(CliExitCodes.Success, ingested);

        var now = DateTimeOffset.UtcNow;
        await using var db = host.Database.CreateContext();
        db.DdragonVersions.Add(new DdragonVersion
        {
            Version = latest,
            Status = DdragonVersionStatus.Ready,
            DiscoveredAt = now,
            UpdatedAt = now,
            ReadyAt = now,
            PromotedAt = now,
        });
        await db.SaveChangesAsync(IngestionHost.Token);
    }
}
