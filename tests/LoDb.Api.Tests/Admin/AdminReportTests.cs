using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// The panels that only read: the donations and their daily series, and the monitoring of
/// the API, its dependencies and its queues.
/// </summary>
public sealed class AdminReportTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const int DonationDays = 30;

    [Fact]
    public async Task DonationsShowTheirTotalsAndTheirDailySeries()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var donor = await App.SeedAsync(new AccountSeed());
        await AdminSeed.DonationAsync(App, donor.Id, 500);
        await AdminSeed.DonationAsync(App, donorId: null, 1500);

        var page = await ReadAsync(admin, "/api/admin/donations");

        var kpis = page.GetProperty("kpis");
        Assert.Equal(
            (2000L, 2, 1, 1),
            (kpis.GetProperty("totalCents").GetInt64(), kpis.GetProperty("count").GetInt32(),
                kpis.GetProperty("identifiedDonors").GetInt32(),
                kpis.GetProperty("anonymous").GetInt32()));
        var daily = page.GetProperty("daily");
        Assert.Equal(DonationDays, daily.GetArrayLength());
        Assert.Equal(2000L, daily[DonationDays - 1].GetProperty("cents").GetInt64());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item =>
            item.GetProperty("donor").ValueKind == System.Text.Json.JsonValueKind.Object
            && ApiJson.Text(item.GetProperty("donor"), "username") == AccountSeed.Name);
    }

    [Fact]
    public async Task MonitoringProbesTheDependenciesAndCountsTheApplication()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await App.SeedAsync(new AccountSeed());

        var report = await ReadAsync(admin, "/api/admin/monitoring?refresh=true");

        var services = report.GetProperty("services").EnumerateArray()
            .ToDictionary(static probe => ApiJson.Text(probe, "name")!);
        Assert.Equal("ok", ApiJson.Text(services["postgres"], "status"));
        Assert.False(string.IsNullOrEmpty(ApiJson.Text(services["postgres"], "version")));
        Assert.Equal("ok", ApiJson.Text(services["storage"], "status"));
        var counters = report.GetProperty("counters");
        Assert.Equal(2, counters.GetProperty("usersTotal").GetInt32());
        var ingestion = report.GetProperty("ingestion");
        Assert.Equal(0, ingestion.GetProperty("versionBacklog").GetInt32());
        Assert.Equal(0, ingestion.GetProperty("outboxDead").GetInt32());
        var process = report.GetProperty("process");
        Assert.False(string.IsNullOrEmpty(ApiJson.Text(process, "version")));
        Assert.True(process.GetProperty("workingSetBytes").GetInt64() > 0);
        var tables = report.GetProperty("tables").EnumerateArray()
            .ToDictionary(
                static table => ApiJson.Text(table, "name")!,
                static table => table.GetProperty("bytes").GetInt64());
        // Every table listed exists; a plain one weighs its own pages.
        Assert.Equal(11, tables.Count);
        Assert.Contains("analytics_event", tables.Keys);
        Assert.True(tables["users"] > 0);
    }

    [Fact]
    public async Task MonitoringIsKeptUntilARefreshIsAsked()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var first = await ReadAsync(admin, "/api/admin/monitoring?refresh=true");
        await App.SeedAsync(new AccountSeed());

        var cached = await ReadAsync(admin, "/api/admin/monitoring");
        var refreshed = await ReadAsync(admin, "/api/admin/monitoring?refresh=true");

        Assert.Equal(1, Users(first));
        Assert.Equal(1, Users(cached));
        Assert.Equal(2, Users(refreshed));
    }

    private static int Users(System.Text.Json.JsonElement report) =>
        report.GetProperty("counters").GetProperty("usersTotal").GetInt32();
}
