using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Import;
using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Tests.Analytics.Support;
using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Analytics.Import;

/// <summary>
/// The takeover of the legacy day files: each day imported once, as written by PHP (its
/// empty maps as <c>[]</c>, its numeric visitor ids), never over an existing row.
/// </summary>
public sealed class LegacyDailyImportTests(PostgresContainerFixture postgres)
    : AnalyticsDatabase(postgres)
{
    private static readonly DateOnly First = new(2026, 7, 14);
    private static readonly DateOnly Last = new(2026, 7, 16);

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lodb-analytics-");

    private ILegacyDailyImport Import => Get<ILegacyDailyImport>();

    [Fact]
    public async Task EachLegacyDayIsImportedOnce()
    {
        CopyLegacyDays();

        var report = await Import.ImportAsync(_folder.FullName, dryRun: false, Cancellation);
        var again = await Import.ImportAsync(_folder.FullName, dryRun: false, Cancellation);

        Assert.Equal(new DailyImportReport
        {
            Files = 3, Imported = 3, AlreadyPresent = 0, Refused = 0,
        }, report);
        Assert.Equal((0, 3), (again.Imported, again.AlreadyPresent));
        var rows = await Get<DailyStore>().ReadAsync(First, Last, Cancellation);
        Assert.All(rows, static row => Assert.Equal(AnalyticsDailySource.Import, row.Source));
        foreach (var row in rows)
        {
            var file = Path.Combine(LegacySamples.DailyFolder, $"{row.Day:yyyy-MM-dd}.json");
            var expected = await LegacyDailyFile.ReadAsync(file, Cancellation);
            AggregateAssert.Equal(expected!, DailyColumns.Read(row));
        }
    }

    [Fact]
    public async Task DryRunCountsWithoutWriting()
    {
        CopyLegacyDays();

        var report = await Import.ImportAsync(_folder.FullName, dryRun: true, Cancellation);

        Assert.Equal(3, report.Imported);
        Assert.Empty(await Get<DailyStore>().ReadAsync(First, Last, Cancellation));
    }

    [Fact]
    public async Task DayWithAnAggregateIsLeftAlone()
    {
        CopyLegacyDays();
        var store = Get<DailyStore>();
        await store.UpsertEventsAsync(new DailyAggregate(Last) { Views = 1 }, Cancellation);

        var report = await Import.ImportAsync(_folder.FullName, dryRun: false, Cancellation);

        Assert.Equal((2, 1), (report.Imported, report.AlreadyPresent));
        var kept = Assert.Single(await store.ReadAsync(Last, Last, Cancellation));
        Assert.Equal(AnalyticsDailySource.Events, kept.Source);
        Assert.Equal(1, DailyColumns.Read(kept).Views);
    }

    [Fact]
    public async Task PhpQuirksAreRead()
    {
        Write("2026-07-20.json", """
            {"date":"2026-07-20","views":2,"botViews":0,"visitors":[1234567890123456,"ab"],
             "countryNames":[],"pages":[],"byType":{"item":2},"status":{"200":2}}
            """);

        await Import.ImportAsync(_folder.FullName, dryRun: false, Cancellation);

        var day = new DateOnly(2026, 7, 20);
        var row = Assert.Single(await Get<DailyStore>().ReadAsync(day, day, Cancellation));
        var daily = DailyColumns.Read(row);
        Assert.Equal(["1234567890123456", "ab"], daily.Visitors);
        Assert.Empty(daily.Buckets[DailyBuckets.Pages]);
        Assert.Equal(2, daily.Buckets[DailyBuckets.Status]["200"]);
    }

    [Fact]
    public async Task WhatIsNoDayAggregateIsRefused()
    {
        Write("notes.json", "{}");
        Write("2026-02-30.json", "{}");
        Write("2026-07-17.json", "[1]");
        Write("2026-07-18.json", """{"date":"2026-07-19","views":1}""");
        Write("2026-07-19.json", "not json");
        Write("2026-07-21.txt", "{}");

        var report = await Import.ImportAsync(_folder.FullName, dryRun: false, Cancellation);

        Assert.Equal((5, 0, 5), (report.Files, report.Imported, report.Refused));
    }

    [Fact]
    public async Task MissingDirectoryIsReported()
    {
        var missing = Path.Combine(_folder.FullName, "missing");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => Import.ImportAsync(missing, dryRun: false, Cancellation));
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        await base.DisposeServicesAsync();
        _folder.Delete(recursive: true);
    }

    private void CopyLegacyDays()
    {
        foreach (var file in Directory.EnumerateFiles(LegacySamples.DailyFolder))
        {
            File.Copy(file, Path.Combine(_folder.FullName, Path.GetFileName(file)));
        }
    }

    private void Write(string name, string content) =>
        File.WriteAllText(Path.Combine(_folder.FullName, name), content);
}
