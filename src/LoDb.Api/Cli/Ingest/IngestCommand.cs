using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Pipeline.Versions;

namespace LoDb.Api.Cli.Ingest;

/// <summary>
/// <c>ingest</c>: ingests versions now, as the patch watch would, one after the other.
/// </summary>
/// <remarks>
/// <para>
/// <c>ingest (--version &lt;x.y.z&gt; | --latest &lt;n&gt;) [--languages all|&lt;codes&gt;]
/// [--force]</c>. Only a run over every language moves the version's state and may promote
/// it; <c>--force</c> fetches every image again and replaces the manifest rows whose verdict
/// changed. Stored datasets and blobs are kept either way. Host settings come as
/// <c>--key=value</c>, never right after <c>--force</c>: the host's command-line reader
/// would take the setting for the switch's value.
/// </para>
/// <para>
/// Exit code 0 when every version completed, 1 when one did not (upstream failure, unknown
/// version, run held elsewhere), 2 on invalid arguments. The outcome of each version is its
/// summary log line.
/// </para>
/// </remarks>
internal sealed class IngestCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "ingest";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        IngestArguments options;
        try
        {
            options = IngestArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"{exception.Message} {IngestArguments.Usage}");
            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var ingestion = services.GetRequiredService<IVersionIngestion>();
        var request = new IngestionRequest { Languages = options.Languages, Force = options.Force };
        var completed = true;
        foreach (var version in await VersionsAsync(options, cancellationToken))
        {
            var result = await ingestion.IngestAsync(version, request, cancellationToken);
            completed &= result.Outcome == VersionIngestionOutcome.Completed;
        }

        return completed ? CliExitCodes.Success : CliExitCodes.Failure;
    }

    private async Task<IReadOnlyList<PatchVersion>> VersionsAsync(
        IngestArguments options,
        CancellationToken cancellationToken)
    {
        if (options.Version is { } version)
        {
            return [version];
        }

        var client = services.GetRequiredService<IDdragonClient>();
        var versions = await client.GetVersionsAsync(cancellationToken);
        return [.. versions.Take(options.Latest)];
    }
}
