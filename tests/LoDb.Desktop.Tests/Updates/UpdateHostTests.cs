using System.Text.Json.Nodes;
using LoDb.Desktop.Bridge;
using LoDb.Desktop.Hosting;
using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Shell;
using LoDb.Desktop.Smoke;
using LoDb.Desktop.Tests.Support;
using LoDb.Desktop.Updates;
using LoDb.Desktop.Updates.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Desktop.Tests.Updates;

/// <summary>Velopack in the loopback host: the bridge, the smoke report and the stop.</summary>
public sealed class UpdateHostTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly FakeUpdateEngine _engine = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task ReplacesTheDefaultAndReportsNoUpdateWithoutAnInstall()
    {
        await using var host = await DesktopTestHost.StartAsync(
            new DesktopHostSetup { ApiOrigin = new Uri("http://127.0.0.1:1") },
            Token);

        var updates = host.Services.GetRequiredService<IDesktopUpdates>();

        Assert.IsType<VelopackUpdates>(updates);
        Assert.Equal(UpdateSnapshot.None, updates.Current);
    }

    [Fact]
    public async Task ShowsTheReadyVersionToTheBridgeAndTheSmokeReport()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");
        await using var host = await StartAsync(UpdateMode.ApplyAtExit);
        using var loopback = new HttpClient { BaseAddress = host.Address };

        var reply = await host.Services.GetRequiredService<DesktopBridge>().HandleAsync(
            "{\"id\":\"u1\",\"type\":\"updateState\",\"payload\":{}}",
            Token);
        var smoke = new SmokeChecks(host, loopback).CheckUpdates();

        var result = JsonNode.Parse(reply!)!["result"]!;
        Assert.Equal("ready", (string?)result["state"]);
        Assert.Equal("2.0.0", (string?)result["version"]);
        Assert.Equal("updates: ready 2.0.0", smoke.ToString());
    }

    [Fact]
    public async Task HandsTheReadyUpdateToTheUpdaterAsTheHostStops()
    {
        _engine.Newer = FakeUpdateEngine.Release("2.0.0");

        var host = await StartAsync(UpdateMode.ApplyAtExit);
        await host.DisposeAsync();

        Assert.Equal(["at-exit 2.0.0"], _engine.HandOvers);
    }

    private Task<LoopbackHost> StartAsync(UpdateMode mode) => LoopbackHost.StartAsync(
        new DesktopOptions
        {
            Version = "1.0.0",
            Channel = "stable",
            ApiOrigin = new Uri("http://127.0.0.1:1"),
            ShellDirectory = TestShell.CreateIn(_folder),
            DataDirectory = _folder.Combine("data"),
        },
        services =>
        {
            services.AddSingleton<IDesktopShell>(new FakeShell());
            services.AddSingleton<ISystemBrowser>(new FakeBrowser());
            services.AddSingleton(new UpdateSettings { Mode = mode });
            services.AddSingleton<IUpdateEngine>(_engine);
        },
        Token);
}
