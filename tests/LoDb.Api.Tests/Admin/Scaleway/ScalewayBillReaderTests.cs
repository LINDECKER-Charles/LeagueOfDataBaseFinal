using System.Text.Json.Nodes;
using LoDb.Api.Modules.Admin.Scaleway;
using LoDb.Api.Modules.Admin.Scaleway.Views;
using LoDb.Api.Tests.Admin.Scaleway.Support;

namespace LoDb.Api.Tests.Admin.Scaleway;

/// <summary>
/// The bill as the reader assembles it from Scaleway's lines: each project's months, their
/// totals with the deductions, every page of a long list, and one reading an hour shared by
/// the requests that overlap.
/// </summary>
public sealed class ScalewayBillReaderTests : IDisposable
{
    private const int Readers = 8;

    private readonly ScalewayHarness _harness = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachProjectGetsItsLinesAndTheirSumDeductionsIncluded()
    {
        var bill = await _harness.Reader.ReadAsync(Token);

        Assert.True(bill.Configured);
        Assert.False(bill.Stale);
        Assert.Equal(_harness.Clock.GetUtcNow(), bill.FetchedAt);
        Assert.Equal("EUR", bill.Currency);
        var backups = Current(bill.Backups);
        Assert.Equal(Guid.Parse(ScalewayAnswers.BackupsProjectId), bill.Backups!.ProjectId);
        Assert.Equal(0m, backups.Total);
        Assert.Equal(6, backups.Lines.Count);
        Assert.Contains(backups.Lines, static line => line.Amount == -0.01m && line.Quantity == -1);
        var pages = Current(bill.Pages);
        Assert.Equal(1.2346m, pages.Total);
        var first = pages.Lines[0];
        Assert.Equal(
            ("Standard One Zone", "One Zone - fr-par", 112230m, "gigabyte_hour", 1.2346m),
            (first.Product, first.Resource, first.Quantity, first.Unit, first.Amount));
        Assert.Equal([1.2346m, 0.015m, -0.015m], pages.Lines.Select(static line => line.Amount));
    }

    [Fact]
    public async Task EachMonthOfTheHalfYearIsAskedForTheOrganizationsObjectStorage()
    {
        await _harness.Reader.ReadAsync(Token);

        var calls = _harness.Scaleway.Calls.ToList();
        Assert.Equal(
            ["2026-10", "2026-09", "2026-08", "2026-07", "2026-06", "2026-05"],
            calls.Select(static call => call.Period));
        Assert.All(calls, static call =>
        {
            Assert.Equal("https", call.Url.Scheme);
            Assert.Equal("api.scaleway.com", call.Url.Host);
            Assert.Equal("/billing/v2beta1/consumptions", call.Url.AbsolutePath);
            Assert.Equal(ScalewayAnswers.OrganizationId, call.Query("organization_id"));
            Assert.Equal("Object Storage", call.Query("category_name"));
            Assert.Equal(1, call.Page);
            Assert.Equal("100", call.Query("page_size"));
            Assert.Equal(ScalewayHarness.Key, call.Token);
        });
    }

    [Fact]
    public async Task EveryPageOfAMonthIsRead()
    {
        var lines = ScalewayAnswers.Lines();
        _harness.Scaleway.Answer = call => call.Period != ScalewayAnswers.CurrentPeriod
            ? ScalewayAnswers.Empty()
            : ScalewayAnswers.Page(PageOf(lines, call.Page), lines.Count);

        var bill = await _harness.Reader.ReadAsync(Token);

        Assert.Equal(
            [1, 2, 3, 4],
            _harness.Scaleway.Calls
                .Where(static call => call.Period == ScalewayAnswers.CurrentPeriod)
                .Select(static call => call.Page));
        Assert.Equal(6, Current(bill.Backups).Lines.Count);
        Assert.Equal(1.2346m, Current(bill.Pages).Total);
    }

    [Fact]
    public async Task ProjectWithoutConsumptionHasSixEmptyMonths()
    {
        _harness.Scaleway.Answer = static _ => ScalewayAnswers.Empty();

        var bill = await _harness.Reader.ReadAsync(Token);

        Assert.Equal("EUR", bill.Currency);
        Assert.Equal(6, bill.Backups!.Periods.Count);
        Assert.All(bill.Backups.Periods.Concat(bill.Pages!.Periods), static period =>
        {
            Assert.Equal(0m, period.Total);
            Assert.Empty(period.Lines);
        });
    }

    [Fact]
    public async Task BillIsKeptAnHourThenReadAgain()
    {
        await _harness.Reader.ReadAsync(Token);
        _harness.Clock.Advance(TimeSpan.FromMinutes(59));
        await _harness.Reader.ReadAsync(Token);
        Assert.Equal(6, _harness.Scaleway.Calls.Count);

        _harness.Clock.Advance(TimeSpan.FromMinutes(1));
        var bill = await _harness.Reader.ReadAsync(Token);

        Assert.Equal(12, _harness.Scaleway.Calls.Count);
        Assert.Equal(_harness.Clock.GetUtcNow(), bill.FetchedAt);
        var reads = _harness.Logs.GetSnapshot()
            .Where(static log => log.Id.Name == "admin.scaleway_bill.read")
            .ToList();
        Assert.Equal(2, reads.Count);
        Assert.All(reads, static log =>
            Assert.StartsWith(
                "Read the Scaleway bill of 6 months: 9 lines",
                log.Message,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task NewMonthStartsANewReadingWithinTheHour()
    {
        _harness.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 31, 23, 50, 0, TimeSpan.Zero));
        var october = await _harness.Reader.ReadAsync(Token);
        _harness.Clock.Advance(TimeSpan.FromMinutes(20));

        var november = await _harness.Reader.ReadAsync(Token);

        Assert.Equal("2026-10", october.Pages!.Periods[0].Period);
        Assert.Equal("2026-11", november.Pages!.Periods[0].Period);
        Assert.Equal(_harness.Clock.GetUtcNow(), november.FetchedAt);
        Assert.Equal(12, _harness.Scaleway.Calls.Count);
    }

    [Fact]
    public async Task SixMonthsAreAskedAtOnce()
    {
        _harness.Scaleway.Gate =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = _harness.Reader.ReadAsync(Token);

        // Every month is asked while none has answered yet.
        await WaitForCallsAsync(BillingPeriods.Count);
        _harness.Scaleway.Gate.SetResult();
        var bill = await reading;

        Assert.False(bill.Stale);
        Assert.Equal(1.2346m, Current(bill.Pages).Total);
    }

    [Fact]
    public async Task SlowMonthEndsTheReadingAtItsDeadline()
    {
        _harness.Scaleway.Gate =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _harness.Scaleway.IsHeld = static call => call.Period == "2026-09";
        var reading = _harness.Reader.ReadAsync(Token);
        await WaitForCallsAsync(BillingPeriods.Count);

        _harness.Clock.Advance(ScalewayBillReader.Deadline - TimeSpan.FromSeconds(1));
        Assert.False(reading.IsCompleted);
        _harness.Clock.Advance(TimeSpan.FromSeconds(1));
        var bill = await reading.WaitAsync(TimeSpan.FromSeconds(10), Token);

        Assert.True(bill.Stale);
        Assert.Null(bill.Pages);
        Assert.True(ScalewayBillReader.Deadline < TimeSpan.FromSeconds(30));
        var log = Assert.Single(_harness.Logs.GetSnapshot());
        Assert.Equal("admin.scaleway_bill.unavailable", log.Id.Name);
        Assert.IsAssignableFrom<OperationCanceledException>(log.Exception);
    }

    [Fact]
    public async Task OverlappingRequestsShareOneReadingThatNoneCancels()
    {
        _harness.Scaleway.Gate =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var leaving = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var left = _harness.Reader.ReadAsync(leaving.Token);
        var readers = Enumerable.Range(0, Readers)
            .Select(_ => _harness.Reader.ReadAsync(Token))
            .ToList();
        await WaitForCallsAsync(1);

        await leaving.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => left);
        _harness.Scaleway.Gate.SetResult();
        var bills = await Task.WhenAll(readers);

        Assert.Equal(6, _harness.Scaleway.Calls.Count);
        Assert.All(bills, static bill => Assert.Equal(1.2346m, Current(bill.Pages).Total));
    }

    [Fact]
    public async Task UnsetKeyReadsNothing()
    {
        using var harness = new ScalewayHarness(isConfigured: false);

        var bill = await harness.Reader.ReadAsync(Token);

        Assert.Equal(ScalewayBillReport.NotConfigured, bill);
        Assert.False(bill.Configured);
        Assert.Null(bill.Backups);
        Assert.Empty(harness.Scaleway.Calls);
    }

    [Fact]
    public void PeriodsAreTheSixCalendarMonthsEndingWithTheCurrentOne()
    {
        var periods = BillingPeriods.EndingAt(
            new DateTimeOffset(2026, 2, 1, 0, 30, 0, TimeSpan.FromHours(2)));

        Assert.Equal(["2026-01", "2025-12", "2025-11", "2025-10", "2025-09", "2025-08"], periods);
    }

    public void Dispose() => _harness.Dispose();

    private static ScalewayBillPeriod Current(ScalewayProjectBill? project)
    {
        Assert.NotNull(project);
        var period = project.Periods[0];
        Assert.Equal(ScalewayAnswers.CurrentPeriod, period.Period);
        return period;
    }

    private static List<JsonNode?> PageOf(JsonArray lines, int page)
    {
        const int Size = 3;
        return [.. lines.Skip((page - 1) * Size).Take(Size)];
    }

    private async Task WaitForCallsAsync(int count)
    {
        while (_harness.Scaleway.Calls.Count < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), Token);
        }
    }
}
