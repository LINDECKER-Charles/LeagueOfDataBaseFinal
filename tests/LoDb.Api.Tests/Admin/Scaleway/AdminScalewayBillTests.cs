using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Scaleway;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Scaleway.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Scaleway;

/// <summary>
/// <c>GET /api/admin/scaleway/bill</c> as the panels read it: the bill of the backups and of
/// the pages, month by month, or a word saying it is not configured.
/// </summary>
public sealed class AdminScalewayBillTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string BillPath = "/api/admin/scaleway/bill";

    private readonly FakeScaleway _scaleway = new();
    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    public async ValueTask InitializeAsync()
    {
        _app = await AccountsApp.StartAsync(postgres, ScalewayHarness.Settings(), services =>
            services.AddHttpClient(ScalewayBillingClient.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _scaleway));
        // The fixture's month must be the host's: its clock starts at the real time.
        _scaleway.Answer = call => call.Period == CurrentPeriod()
            ? ScalewayAnswers.Page([.. ScalewayAnswers.Lines()], totalCount: null)
            : ScalewayAnswers.Empty();
    }

    [Fact]
    public async Task AdministratorReadsTheBillOfBothProjects()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        using var response = await admin.GetAsync(BillPath);

        var bill = await ApiJson.ReadAsync(response, HttpStatusCode.OK);

        Assert.True(bill.GetProperty("configured").GetBoolean());
        Assert.False(bill.GetProperty("stale").GetBoolean());
        Assert.Equal("EUR", ApiJson.Text(bill, "currency"));
        Assert.Equal(JsonValueKind.String, bill.GetProperty("fetchedAt").ValueKind);
        Assert.False(bill.TryGetProperty("updatedAt", out _));
        var pages = bill.GetProperty("pages");
        Assert.Equal(ScalewayAnswers.PagesProjectId, ApiJson.Text(pages, "projectId"));
        var month = pages.GetProperty("periods")[0];
        Assert.Equal(CurrentPeriod(), ApiJson.Text(month, "period"));
        Assert.Equal(1.2346m, month.GetProperty("total").GetDecimal());
        var line = month.GetProperty("lines")[0];
        Assert.Equal("Standard One Zone", ApiJson.Text(line, "product"));
        Assert.Equal("One Zone - fr-par", ApiJson.Text(line, "resource"));
        Assert.Equal(112230m, line.GetProperty("quantity").GetDecimal());
        Assert.Equal("gigabyte_hour", ApiJson.Text(line, "unit"));
        Assert.Equal(1.2346m, line.GetProperty("amount").GetDecimal());
        Assert.Equal(6, bill.GetProperty("backups").GetProperty("periods").GetArrayLength());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _scaleway.Dispose();
    }

    private string CurrentPeriod() =>
        BillingPeriods.EndingAt(App.Clock.GetUtcNow())[0];
}
