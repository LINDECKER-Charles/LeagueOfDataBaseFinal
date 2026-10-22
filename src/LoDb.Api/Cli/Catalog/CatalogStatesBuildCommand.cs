using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Api.Cli.Catalog;

/// <summary>
/// <c>catalog states build</c>: catches the index of states up to the latest promoted version
/// (docs/produit/seo-une-page-par-etat.md, § 3.2), before the API serves it.
/// </summary>
/// <remarks>
/// <para>
/// <c>catalog states build [--rebuild] [--ingest-missing]</c>, run by the one-shot Compose
/// service <c>states</c>, which the API waits for. It does nothing when the current generation
/// of the compiled fingerprint scheme covers every version up to the latest; extends it when
/// only the latest moved; builds a new generation, then makes it current, when none is, when
/// the coverage start moved, or with <c>--rebuild</c>. It reads the stored en_US datasets and
/// downloads none, unless <c>--ingest-missing</c> first ingests those of every listed version
/// up to the latest. One summary line per update, one per batch of 50 versions.
/// </para>
/// <para>
/// Exit code 0 once the index is caught up, or when Data Dragon's list of versions cannot be
/// read without <c>--ingest-missing</c> (one warning line; the API's reconciliation catches up
/// later); 1 when another writer kept the index's lock for a minute, or on any other failure;
/// 2 on invalid arguments. Host settings come as <c>--key=value</c>, never right after a
/// switch: the host's command-line reader would take the setting for the switch's value.
/// </para>
/// </remarks>
internal sealed partial class CatalogStatesBuildCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "catalog states build";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (await ParseAsync(arguments) is not { } options)
        {
            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var updater = services.GetRequiredService<IStateIndexUpdater>();
        var request = new StateIndexRequest
        {
            Trigger = StateIndexTrigger.Build,
            ShouldRebuild = options.ShouldRebuild,
            ShouldIngestMissing = options.ShouldIngestMissing,
        };
        try
        {
            var report = await updater.UpdateAsync(request, cancellationToken);
            return report.Outcome == StateIndexOutcome.Locked
                ? CliExitCodes.Failure
                : CliExitCodes.Success;
        }
        catch (EgressException exception) when (!options.ShouldIngestMissing)
        {
            var logger = services.GetRequiredService<ILogger<CatalogStatesBuildCommand>>();
            LogDeferred(logger, exception);
            return CliExitCodes.Success;
        }
    }

    // Null once the usage is printed.
    private static async Task<CatalogStatesBuildArguments?> ParseAsync(
        IReadOnlyList<string> arguments)
    {
        try
        {
            return CatalogStatesBuildArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            var usage = CatalogStatesBuildArguments.Usage;
            await Console.Error.WriteLineAsync($"{exception.Message} {usage}");
            return null;
        }
    }

    [LoggerMessage(
        EventName = "states.build.deferred",
        Level = LogLevel.Warning,
        Message = "Data Dragon could not be read: the index of states is left as it is, for the"
            + " API's reconciliation to catch up.")]
    private static partial void LogDeferred(ILogger logger, Exception exception);
}
