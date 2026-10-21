using LoDb.Api.Cli;
using LoDb.Api.Cli.Prerender;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Speed;

/// <summary>
/// <c>prerender pause</c> and <c>prerender resume</c> around a deploy: the running pass paused
/// with the command line's reason, then resumed; nothing to do is no failure; a pause an
/// operator chose stays unless <c>--any</c>; an unknown argument is a usage error.
/// </summary>
public sealed class PrerenderPauseCommandTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.5.1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PauseThenResumeBringsTheRunningPassBack()
    {
        await using var harness = await StartAsync();
        var passId = await harness.LaunchAsync(Scope());

        var paused = await new PrerenderPauseCommand(harness.Services).ExecuteAsync([], Token);
        var whilePaused = await harness.PassAsync(passId);
        var resumed = await new PrerenderResumeCommand(harness.Services).ExecuteAsync([], Token);

        Assert.Equal((CliExitCodes.Success, CliExitCodes.Success), (paused, resumed));
        Assert.Equal(
            (PrerenderPassState.Paused, ActivePassSwitch.DeployReason),
            (whilePaused.State, whilePaused.PauseReason));
        var after = await harness.PassAsync(passId);
        Assert.Equal((PrerenderPassState.Running, null), (after.State, after.PauseReason));
    }

    [Fact]
    public async Task NothingToPauseOrResumeIsNoFailure()
    {
        await using var harness = await StartAsync();

        var paused = await new PrerenderPauseCommand(harness.Services).ExecuteAsync([], Token);
        var resumed = await new PrerenderResumeCommand(harness.Services).ExecuteAsync([], Token);

        Assert.Equal((CliExitCodes.Success, CliExitCodes.Success), (paused, resumed));
    }

    [Fact]
    public async Task OperatorsPauseStaysUnlessAnyIsSaid()
    {
        await using var harness = await StartAsync();
        var passId = await harness.LaunchAsync(Scope());
        await harness.Get<PassControl>().PauseAsync(passId, Token);
        var command = new PrerenderResumeCommand(harness.Services);

        var refused = await command.ExecuteAsync([], Token);
        var stayed = (await harness.PassAsync(passId)).State;
        var forced = await command.ExecuteAsync(["--any"], Token);

        Assert.Equal((CliExitCodes.Failure, CliExitCodes.Success), (refused, forced));
        Assert.Equal(PrerenderPassState.Paused, stayed);
        Assert.Equal(PrerenderPassState.Running, (await harness.PassAsync(passId)).State);
    }

    [Fact]
    public async Task UnknownArgumentIsAUsageError()
    {
        await using var harness = await StartAsync();

        var pause = await new PrerenderPauseCommand(harness.Services)
            .ExecuteAsync(["--any"], Token);
        var resume = await new PrerenderResumeCommand(harness.Services)
            .ExecuteAsync(["--all", "--ConnectionStrings:LoDb=Host=elsewhere"], Token);

        Assert.Equal((CliExitCodes.Usage, CliExitCodes.Usage), (pause, resume));
    }

    private static PassScope Scope() => PassScope.Of(Version) with { Locales = [UiLocale.En] };

    private async Task<PrerenderHarness> StartAsync()
    {
        var harness = await PrerenderHarness.StartAsync(postgres);
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }
}
