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
/// the coverage start moved back, or with <c>--rebuild</c>; leaves it behind when a listed
/// version after its end lacks its datasets. It reads the stored en_US datasets and
/// downloads none, unless <c>--ingest-missing</c> first ingests those of every listed version
/// up to the latest. One summary line per update, one per batch of 50 versions.
/// </para>
/// <para>
/// Exit code 0 once the index is caught up; 0 as well when, without <c>--ingest-missing</c>,
/// Data Dragon's list of versions cannot be read while a generation is current: the API
/// serves that index, behind, until its reconciliation catches it up (one warning line). With
/// none current (the first deploy, a new fingerprint scheme), the API would serve no index:
/// exit code 1, so that the deploy stops while the old stack still serves (§ 4.1). Exit code
/// 1 as well when another writer kept the index's lock for a minute, or on any other failure;
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
        try
        {
            var report = await updater.UpdateAsync(RequestOf(options), cancellationToken);
            return report.Outcome == StateIndexOutcome.Locked
                ? CliExitCodes.Failure
                : CliExitCodes.Success;
        }
        catch (EgressException exception) when (!options.ShouldIngestMissing)
        {
            return await UnlistedAsync(updater, exception, cancellationToken);
        }
    }

    private static StateIndexRequest RequestOf(CatalogStatesBuildArguments options) => new()
    {
        Trigger = StateIndexTrigger.Build,
        ShouldRebuild = options.ShouldRebuild,
        ShouldIngestMissing = options.ShouldIngestMissing,
    };

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

    // The list of versions could not be read: an index that lags is served as it is, while
    // no index at all must not reach the API.
    private async Task<int> UnlistedAsync(
        IStateIndexUpdater updater,
        EgressException exception,
        CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<CatalogStatesBuildCommand>>();
        if (!await updater.HasCurrentAsync(cancellationToken))
        {
            LogNoIndex(logger, exception);
            return CliExitCodes.Failure;
        }

        LogDeferred(logger, exception);
        return CliExitCodes.Success;
    }

    [LoggerMessage(
        EventName = "states.build.deferred",
        Level = LogLevel.Warning,
        Message = "Data Dragon's list of versions could not be read: the current index of"
            + " states is left as it is, for the API's reconciliation to catch up.")]
    private static partial void LogDeferred(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "states.build.failed",
        Level = LogLevel.Error,
        Message = "Data Dragon's list of versions could not be read, and no index of states"
            + " is current: the API cannot serve without one.")]
    private static partial void LogNoIndex(ILogger logger, Exception exception);
}
