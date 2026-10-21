using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Cli.Prerender;

/// <summary>
/// <c>prerender resume [--any]</c>: resumes, after a deploy, the pass <c>prerender pause</c>
/// paused, and says so in one line; the worker takes it at its next tick.
/// </summary>
/// <remarks>
/// A pass an operator paused in the admin, or that paused itself on a setting that fails every
/// page, stays paused unless <c>--any</c>: a deploy must not undo a decision it did not take.
/// Exit code 0 when the pass runs again, or when none was paused (nothing to resume); 1 when
/// the pause is not the command line's (without <c>--any</c>), the pre-rendering is not
/// configured, or the pass moved meanwhile; 2 on an unknown argument.
/// </remarks>
internal sealed class PrerenderResumeCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "prerender resume";

    private const string Usage = "Usage: prerender resume [--any]";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (ActivePassSwitch.Flags(arguments, ActivePassSwitch.AnyFlag) is not { } flags)
        {
            await Console.Error.WriteLineAsync(Usage);
            return CliExitCodes.Usage;
        }

        if (ActivePassSwitch.Unready(services) is { } problem)
        {
            await Console.Error.WriteLineAsync(problem);
            return CliExitCodes.Failure;
        }

        var pass = await ActivePassSwitch.ActiveAsync(services, cancellationToken);
        if (pass is not { State: PrerenderPassState.Paused })
        {
            await Console.Out.WriteLineAsync(pass is null
                ? "No pass is active: nothing to resume."
                : $"Pass {pass.Id} (epoch {pass.Epoch}) runs already: nothing to do.");
            return CliExitCodes.Success;
        }

        var isAnyPause = flags.Contains(ActivePassSwitch.AnyFlag);
        return await ResumeAsync(pass, isAnyPause, cancellationToken);
    }

    private async Task<int> ResumeAsync(
        PrerenderPass pass,
        bool isAnyPause,
        CancellationToken cancellationToken)
    {
        if (pass.PauseReason != ActivePassSwitch.DeployReason && !isAnyPause)
        {
            await Console.Error.WriteLineAsync(
                $"Pass {pass.Id} was not paused by prerender pause: left paused (--any resumes"
                + " it anyway).");
            return CliExitCodes.Failure;
        }

        var change = await services.GetRequiredService<PassControl>()
            .ResumeAsync(pass.Id, cancellationToken);
        return await ReportAsync(pass, change);
    }

    private static async Task<int> ReportAsync(PrerenderPass pass, PassChange change)
    {
        if (change != PassChange.Changed)
        {
            await Console.Error.WriteLineAsync(
                $"Pass {pass.Id} moved meanwhile and was not resumed: look at the admin.");
            return CliExitCodes.Failure;
        }

        await Console.Out.WriteLineAsync(
            $"Pass {pass.Id} (epoch {pass.Epoch}) resumed: the worker takes it at its next"
            + " tick.");
        return CliExitCodes.Success;
    }
}
