using System.Net;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// A transient failure stores nothing half-read and leaves the version to a later attempt,
/// which resumes it; the attempts are bounded.
/// </summary>
public sealed class VersionRetryTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Latest = DdragonFixtures.Latest;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(10);

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task TransientFailureLeavesTheVersionForLater()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        instance.Replay.FailWith(
            VersionIngestionTests.IsAhriPortrait,
            HttpStatusCode.ServiceUnavailable);
        using var failures = VersionIngestionTests.Collect(
            instance,
            "lodb.ingestion.fetch.failures");

        var failed = await VersionIngestionTests.IngestAsync(instance, Latest);
        var dueAtOnce = await instance.Get<VersionStates>().DueAsync(Token);
        harness.Clock.Advance(RetryDelay);
        var dueLater = await instance.Get<VersionStates>().DueAsync(Token);

        Assert.Equal(VersionIngestionOutcome.Incomplete, failed.Outcome);
        Assert.Equal(1, failed.Images?.Failed);
        Assert.False(failed.Promoted);
        Assert.Equal(1, Assert.Single(failures.GetMeasurementSnapshot()).Value);
        var warning = Assert.Single(
            instance.Logs,
            static log => log.Id.Name == "ingest.version.incomplete");
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Empty(dueAtOnce);
        Assert.Equal([Latest], dueLater);
    }

    [Fact]
    public async Task NextAttemptFetchesOnlyWhatFailed()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var first = harness.StartInstance();
        first.Replay.FailWith(
            VersionIngestionTests.IsAhriPortrait,
            HttpStatusCode.ServiceUnavailable);
        await VersionIngestionTests.IngestAsync(first, Latest);
        harness.Clock.Advance(RetryDelay);
        var second = harness.StartInstance();

        var resumed = await VersionIngestionTests.IngestAsync(second, Latest);

        Assert.Equal(VersionIngestionOutcome.Completed, resumed.Outcome);
        Assert.True(resumed.Promoted);
        Assert.Equal(0, resumed.DatasetsWritten);
        Assert.Equal(
            ["/cdn/16.19.1/img/champion/Ahri.png"],
            second.RequestedPaths(static url =>
                url.AbsolutePath.Contains("/img/", StringComparison.Ordinal)));
        var row = await harness.VersionRowAsync(Latest.Value);
        Assert.Equal(DdragonVersionStatus.Ready, row?.Status);
        Assert.Equal(2, row?.Attempts);
    }

    [Fact]
    public async Task FailedAttemptIsRecordedWithItsNextDate()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        instance.Replay.FailWith(
            VersionIngestionTests.IsAhriPortrait,
            HttpStatusCode.ServiceUnavailable);

        await VersionIngestionTests.IngestAsync(instance, Latest);

        var row = await harness.VersionRowAsync(Latest.Value);
        Assert.NotNull(row);
        Assert.Equal(DdragonVersionStatus.Discovered, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal(IngestionHarness.Start + RetryDelay, row.NextAttemptAt);
        Assert.Null(row.ReadyAt);
        Assert.Null(row.PromotedAt);
        Assert.DoesNotContain(
            await harness.AssetRowsAsync(Latest.Value),
            static asset => asset is { Type: ManifestTypes.Champion, Key: "Ahri.png" });
    }

    [Fact]
    public async Task AttemptsAreBounded()
    {
        await using var harness = await IngestionHarness.CreateAsync(
            postgres,
            new Dictionary<string, string?> { ["LoDb:Ingestion:MaxAttempts"] = "2" });
        var instance = harness.StartInstance();
        instance.Replay.FailWith(
            VersionIngestionTests.IsAhriPortrait,
            HttpStatusCode.ServiceUnavailable);

        await VersionIngestionTests.IngestAsync(instance, Latest);
        harness.Clock.Advance(RetryDelay);
        await VersionIngestionTests.IngestAsync(instance, Latest);
        harness.Clock.Advance(TimeSpan.FromDays(1));
        var due = await instance.Get<VersionStates>().DueAsync(Token);

        var row = await harness.VersionRowAsync(Latest.Value);
        Assert.NotNull(row);
        Assert.Equal(DdragonVersionStatus.Failed, row.Status);
        Assert.Equal(2, row.Attempts);
        Assert.Null(row.NextAttemptAt);
        Assert.Empty(due);
        var abandoned = Assert.Single(
            instance.Logs,
            static log => log.Id.Name == "ingest.version.abandoned");
        Assert.Equal(LogLevel.Warning, abandoned.Level);
    }

    [Fact]
    public async Task RetryDelayDoublesWithEveryAttempt()
    {
        await using var harness = await IngestionHarness.CreateAsync(postgres);
        var instance = harness.StartInstance();
        instance.Replay.FailWith(
            VersionIngestionTests.IsAhriPortrait,
            HttpStatusCode.ServiceUnavailable);

        await VersionIngestionTests.IngestAsync(instance, Latest);
        harness.Clock.Advance(RetryDelay);
        await VersionIngestionTests.IngestAsync(instance, Latest);

        var row = await harness.VersionRowAsync(Latest.Value);
        Assert.Equal(2, row?.Attempts);
        Assert.Equal(harness.Clock.GetUtcNow() + (2 * RetryDelay), row?.NextAttemptAt);
    }
}
