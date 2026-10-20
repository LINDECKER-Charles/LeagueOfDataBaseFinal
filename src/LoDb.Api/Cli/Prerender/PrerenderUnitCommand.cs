using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Locks;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Cli.Prerender;

/// <summary>
/// <c>prerender unit</c>: writes one (version, locale) of an older patch to the page store
/// now, as a pass of its own, and says how it went: the local check of the pass and the setup
/// of its end-to-end test.
/// </summary>
/// <remarks>
/// <para>
/// <c>prerender unit --version &lt;x.y.z&gt; [--locale &lt;code&gt;] [--type &lt;type&gt;]
/// [--epoch &lt;n&gt;] [--wait &lt;seconds&gt;]</c>. The locale defaults to <c>en</c>, the
/// epoch to the served one; <c>--type</c> keeps one type of page, its list and its entities.
/// The pass is opened, then written here, under the lock the worker takes: it is refused while
/// another pass is active or being written.
/// </para>
/// <para>
/// The command's host starts no worker: the unit's catalog is ingested first, waited for, as a
/// command line reads, rather than queued for a worker that never comes. While the unit waits
/// (its catalog not stored in time, its version just promoted away), the command tries again, <c>--wait</c> at most (five minutes by default). A pass it leaves
/// unfinished, waiting or paused, it cancels: no pass of its own stays active behind it.
/// </para>
/// <para>
/// Exit code 0 when every page was written or skipped; 1 when the version is not an older
/// patch, a page failed, the pass did not complete (store or SSR refusing it, catalog not
/// stored in time), or the pre-rendering is not configured; 2 on invalid arguments.
/// </para>
/// </remarks>
internal sealed class PrerenderUnitCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "prerender unit";

    // How often the command looks again at a unit that waits.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        PrerenderUnitArguments options;
        try
        {
            options = PrerenderUnitArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync(
                $"{exception.Message} {PrerenderUnitArguments.Usage}");
            return CliExitCodes.Usage;
        }

        if (Unready() is { } problem)
        {
            await Console.Error.WriteLineAsync(problem);
            return CliExitCodes.Failure;
        }

        return await RunAsync(options, cancellationToken);
    }

    // The module is registered by the admin module; until it is, or until its settings are
    // set, there is nothing to run.
    private string? Unready() =>
        services.GetService<PrerenderEngine>() is null
            ? "The pre-rendering is not part of this API's services."
            : services.GetRequiredService<PrerenderActivation>().IsActive
                ? null
                : "Set the page store, PublicHost and SsrBaseUrl (LoDb:Prerender) first.";

    private async Task<int> RunAsync(
        PrerenderUnitArguments options,
        CancellationToken cancellationToken)
    {
        var locks = services.GetRequiredService<IDistributedLock>();
        await using var held = await locks.TryAcquireAsync(
            PrerenderEngine.LockName,
            cancellationToken);
        if (held is null)
        {
            await Console.Error.WriteLineAsync("Another instance is writing a pass right now.");
            return CliExitCodes.Failure;
        }

        await IngestAsync(options.Version, cancellationToken);
        var served = services.GetRequiredService<IOptions<PrerenderOptions>>().Value.ServedEpoch;
        var launch = new PassLaunch { Epoch = options.Epoch ?? served, Scope = options.Scope };
        if (await services.GetRequiredService<PassLauncher>().StartAsync(launch, cancellationToken)
            is not { } passId)
        {
            await Console.Error.WriteLineAsync(
                "A pass is already active: let it end, or pause or cancel it, first.");
            return CliExitCodes.Failure;
        }

        try
        {
            await WriteAsync(passId, options.Wait, cancellationToken);
        }
        finally
        {
            await EndAsync(passId);
        }

        return await ReportAsync(passId, cancellationToken);
    }

    // A Data Dragon failure is said, and the unit then waits for a catalog that does not come.
    private async Task IngestAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        try
        {
            await services.GetRequiredService<IPrerenderCatalog>()
                .IngestAsync(version, cancellationToken);
        }
        catch (EgressException exception)
        {
            await Console.Error.WriteLineAsync(
                $"Data Dragon failed for {version.Value}: {exception.Message}");
        }
    }

    // Written again while its unit waits, within the wait.
    private async Task WriteAsync(long passId, TimeSpan wait, CancellationToken cancellationToken)
    {
        var engine = services.GetRequiredService<PrerenderEngine>();
        var clock = services.GetRequiredService<TimeProvider>();
        var started = clock.GetTimestamp();
        await engine.DrainAsync(cancellationToken);
        while (await StateOfAsync(passId, cancellationToken) == PrerenderPassState.Running
            && clock.GetElapsedTime(started) < wait)
        {
            await Task.Delay(PollInterval, clock, cancellationToken);
            await engine.DrainAsync(cancellationToken);
        }
    }

    private async Task<PrerenderPassState> StateOfAsync(
        long passId,
        CancellationToken cancellationToken)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<LoDbDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.PrerenderPasses.Where(row => row.Id == passId)
            .Select(static row => row.State)
            .SingleAsync(cancellationToken);
    }

    // Whatever stopped the command, its pass ends with it; even on Ctrl+C, hence no token.
    private async Task EndAsync(long passId)
    {
        var control = services.GetRequiredService<PassControl>();
        if (await control.CancelAsync(passId, CancellationToken.None) == PassChange.Changed)
        {
            await Console.Error.WriteLineAsync(
                $"Pass {passId} did not complete: cancelled, so that it does not stay active.");
        }
    }

    private async Task<int> ReportAsync(long passId, CancellationToken cancellationToken)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<LoDbDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var pass = await db.PrerenderPasses.AsNoTracking()
            .Include(static row => row.Units)
            .SingleAsync(row => row.Id == passId, cancellationToken);
        if (pass.Units.Count == 0)
        {
            await Console.Error.WriteLineAsync(
                "No unit: the version is not an older patch Data Dragon lists.");
            return CliExitCodes.Failure;
        }

        foreach (var unit in pass.Units)
        {
            await Console.Out.WriteLineAsync(Summary(pass, unit));
        }

        var isClean = pass.State == PrerenderPassState.Completed
            && pass.Units.All(static unit =>
                unit.State == PrerenderUnitState.Completed && unit.PagesFailed == 0);
        return isClean ? CliExitCodes.Success : CliExitCodes.Failure;
    }

    private static string Summary(PrerenderPass pass, PrerenderUnit unit) =>
        $"Pass {pass.Id} ({PrerenderColumns.ToText(pass.State)}), epoch {pass.Epoch}:"
        + $" {unit.Version} in {unit.Locale} {PrerenderColumns.ToText(unit.State)},"
        + $" {unit.PagesWritten} written, {unit.PagesSkipped} skipped,"
        + $" {unit.PagesFailed} failed of {unit.PagesTotal}, {unit.BytesWritten} bytes.";
}
