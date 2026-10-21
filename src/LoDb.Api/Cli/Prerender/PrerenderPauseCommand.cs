using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Cli.Prerender;

/// <summary>
/// <c>prerender pause</c>: pauses the running pass from the host, before a deploy, and says so
/// in one line; <c>prerender resume</c> resumes it after.
/// </summary>
/// <remarks>
/// The engine sees the pause before its next page: the pages in flight end, none starts. The
/// pause carries <see cref="ActivePassSwitch.DeployReason"/>, which the admin shows. Exit code
/// 0 when the pass is paused, or when none runs (nothing to pause: a deploy goes on); 1 when the
/// pre-rendering is not configured or the pass moved meanwhile; 2 on an unknown argument.
/// </remarks>
internal sealed class PrerenderPauseCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "prerender pause";

    private const string Usage = "Usage: prerender pause";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (ActivePassSwitch.Flags(arguments) is null)
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
        if (pass is not { State: PrerenderPassState.Running })
        {
            await Console.Out.WriteLineAsync(pass is null
                ? "No pass is active: nothing to pause."
                : $"Pass {pass.Id} (epoch {pass.Epoch}) is paused already: nothing to do.");
            return CliExitCodes.Success;
        }

        var control = services.GetRequiredService<PassControl>();
        var change = await control.PauseItselfAsync(
            pass.Id,
            ActivePassSwitch.DeployReason,
            cancellationToken);
        return await ReportAsync(pass, change);
    }

    private static async Task<int> ReportAsync(PrerenderPass pass, PassChange change)
    {
        if (change != PassChange.Changed)
        {
            await Console.Error.WriteLineAsync(
                $"Pass {pass.Id} moved meanwhile and was not paused: look at the admin.");
            return CliExitCodes.Failure;
        }

        await Console.Out.WriteLineAsync(
            $"Pass {pass.Id} (epoch {pass.Epoch}) paused for a deploy:"
            + $" {pass.PagesWritten} pages written so far; prerender resume resumes it.");
        return CliExitCodes.Success;
    }
}
