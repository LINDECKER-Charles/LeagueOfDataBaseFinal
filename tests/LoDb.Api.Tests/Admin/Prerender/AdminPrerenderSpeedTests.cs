using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Prerender;

/// <summary>
/// The speed setting of the pre-rendering: adaptive until an operator sets it, then stored and
/// applied at once on the instance that answered; a target out of the environment's bounds
/// refused field by field; refused under the kill switch; administrators alone.
/// </summary>
public sealed class AdminPrerenderSpeedTests(PostgresContainerFixture postgres)
{
    private const string Speed = "/api/admin/prerender/speed";
    private const string Section = "LoDb:Prerender:";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NeverSetTheSpeedIsAdaptiveWithTheBoundsOfTheEnvironment()
    {
        await using var app = await AccountsApp.StartAsync(postgres, Settings(), NoDoubles);
        using var admin = await AdminBrowser.OpenAsync(app);

        var report = await ReadAsync(admin);

        var setting = report.GetProperty("setting");
        Assert.Equal("adaptive", ApiJson.Text(setting, "mode"));
        Assert.Equal(240, setting.GetProperty("rendersPerMinute").GetInt32());
        Assert.Equal(JsonValueKind.Null, setting.GetProperty("updatedAt").ValueKind);
        var limits = report.GetProperty("limits");
        Assert.Equal(900, limits.GetProperty("manualMaxRendersPerMinute").GetInt32());
        Assert.Equal(1300, limits.GetProperty("manualWarnRendersPerMinute").GetInt32());
        Assert.Equal(2, limits.GetProperty("renderers").GetInt32());
        Assert.Equal(3, limits.GetProperty("brakeMinutes").GetInt32());
        Assert.False(limits.GetProperty("isCostMeasured").GetBoolean());
        Assert.False(report.GetProperty("isFixed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("resources").ValueKind);
    }

    [Fact]
    public async Task ManualTargetIsStoredAndAppliedAtOnce()
    {
        await using var app = await AccountsApp.StartAsync(postgres, Settings(), NoDoubles);
        using var admin = await AdminBrowser.OpenAsync(app);

        using var set = await PutAsync(admin, new { mode = "manual", rendersPerMinute = 720 });
        using var back = await PutAsync(admin, new { mode = "adaptive" });
        var report = await ReadAsync(admin);

        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.NoContent),
            (set.StatusCode, back.StatusCode));
        var setting = report.GetProperty("setting");
        Assert.Equal(
            ("adaptive", 720),
            (ApiJson.Text(setting, "mode"), setting.GetProperty("rendersPerMinute").GetInt32()));
        Assert.NotEqual(JsonValueKind.Null, setting.GetProperty("updatedAt").ValueKind);
        var applied = app.Services.GetRequiredService<PaceSettings>().Current;
        Assert.Equal(new PaceSetting(PaceChoice.Adaptive, 720), applied with { UpdatedAt = null });
        await using var db = app.Database();
        Assert.Equal(1, await db.PrerenderPaceSettings.CountAsync(Cancellation));
    }

    [Fact]
    public async Task InvalidSpeedNamesEachField()
    {
        await using var app = await AccountsApp.StartAsync(postgres, Settings(), NoDoubles);
        using var admin = await AdminBrowser.OpenAsync(app);

        using var over = await PutAsync(admin, new { mode = "manual", rendersPerMinute = 901 });
        using var none = await PutAsync(admin, new { mode = "manual" });
        using var unknown = await PutAsync(admin, new { mode = "turbo", rendersPerMinute = 0 });

        Assert.Equal(["out-of-range"], (await ApiJson.FieldErrorsAsync(over))["rendersPerMinute"]);
        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(none))["rendersPerMinute"]);
        var errors = await ApiJson.FieldErrorsAsync(unknown);
        Assert.Equal(["malformed"], errors["mode"]);
        Assert.Equal(["out-of-range"], errors["rendersPerMinute"]);
    }

    [Fact]
    public async Task KillSwitchRefusesTheSetting()
    {
        var settings = Settings();
        settings[Section + "Pace:Mode"] = "Fixed";
        await using var app = await AccountsApp.StartAsync(postgres, settings, NoDoubles);
        using var admin = await AdminBrowser.OpenAsync(app);

        using var set = await PutAsync(admin, new { mode = "manual", rendersPerMinute = 300 });

        Assert.Equal(
            "prerender-pace-fixed",
            await ApiJson.ProblemCodeAsync(set, HttpStatusCode.Conflict));
        Assert.True((await ReadAsync(admin)).GetProperty("isFixed").GetBoolean());
    }

    [Fact]
    public async Task OnlyAnAdministratorSetsTheSpeed()
    {
        await using var app = await AccountsApp.StartAsync(postgres, Settings(), NoDoubles);
        using var member = await AdminBrowser.OpenMemberAsync(app);

        using var read = await member.GetAsync(Speed);
        using var set = await PutAsync(member, new { mode = "manual", rendersPerMinute = 300 });

        Assert.Equal("forbidden", await ApiJson.ProblemCodeAsync(read, HttpStatusCode.Forbidden));
        Assert.Equal("forbidden", await ApiJson.ProblemCodeAsync(set, HttpStatusCode.Forbidden));
    }

    private static void NoDoubles(IServiceCollection services)
    {
    }

    private static Dictionary<string, string?> Settings() => new()
    {
        [Section + "PublicHost"] = PrerenderHarness.PublicHost,
        [Section + "SsrBaseUrl"] = PrerenderHarness.SsrBaseUrl,
        [Section + "Renderers"] = "2",
        [Section + "Pace:ManualMaxRendersPerMinute"] = "900",
        [Section + "Endpoint"] = "https://s3.fr-par.scw.cloud",
        [Section + "Bucket"] = "lodb-pages-test",
        [Section + "AccessKey"] = "writer",
        [Section + "SecretKey"] = "writer-secret",
    };

    private static async Task<HttpResponseMessage> PutAsync(BrowserClient browser, object body)
    {
        using var request = browser.Post(Speed, body);
        request.Method = HttpMethod.Put;
        return await browser.SendAsync(request);
    }

    private static async Task<JsonElement> ReadAsync(BrowserClient browser)
    {
        using var response = await browser.GetAsync(Speed);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }
}
