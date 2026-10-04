using System.Text.Json.Nodes;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Export;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Cli.Catalog;

/// <summary>
/// <c>catalog export</c>: writes the canonical projection of a (version, language), the one
/// the parity check compares with the legacy stack (L1.8).
/// </summary>
/// <remarks>
/// <para>
/// <c>catalog export --version &lt;x.y.z&gt; --lang &lt;code&gt; [--output &lt;file&gt;]
/// [--stored-only]</c>. The datasets and images the store lacks are ingested first, as for a
/// detail page; <c>--stored-only</c> reads the store alone and reports the images it lacks as
/// <c>pending</c>. The JSON replaces <c>--output</c> once complete, or goes to the standard
/// output, which the host's log lines share: give a file, or silence the logs with
/// <c>--Logging:LogLevel:Default=None</c>. Host settings come as <c>--key=value</c>, never
/// right after <c>--stored-only</c>: the host's command-line reader would take the setting
/// for the switch's value.
/// </para>
/// <para>
/// Exit code 0 once written; 1 when Data Dragon lists neither the version nor the language,
/// when a stored-only export finds datasets missing, or on an upstream failure; 2 on invalid
/// arguments.
/// </para>
/// </remarks>
internal sealed class CatalogExportCommand(IServiceProvider services) : ICliCommand
{
    private const string PartialSuffix = ".partial";

    public static string Name => "catalog export";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (await ParseAsync(arguments) is not { } options)
        {
            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var demand = options.StoredOnly ? ColdDemand.StoredOnly : ColdDemand.Synchronous;
        var load = await services.GetRequiredService<ICatalogReader>()
            .GetAsync(options.Version, options.Language, demand, cancellationToken);
        if (!load.IsReady)
        {
            await Console.Error.WriteLineAsync(Unavailable(options, load.Status));
            return CliExitCodes.Failure;
        }

        var projection = await services.GetRequiredService<CatalogExport>()
            .ProjectAsync(load.Catalog, demand, cancellationToken);
        await WriteAsync(projection, options.Output, cancellationToken);
        return CliExitCodes.Success;
    }

    // Null once the usage is printed.
    private static async Task<CatalogExportArguments?> ParseAsync(IReadOnlyList<string> arguments)
    {
        try
        {
            return CatalogExportArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            var usage = CatalogExportArguments.Usage;
            await Console.Error.WriteLineAsync($"{exception.Message} {usage}");
            return null;
        }
    }

    private static string Unavailable(CatalogExportArguments options, CatalogLoadStatus status)
    {
        var scope = $"{options.Version.Value} {options.Language.Code}";
        return status == CatalogLoadStatus.Unknown
            ? $"Data Dragon lists no catalog {scope}."
            : $"The datasets of {scope} are not stored.";
    }

    private static async Task WriteAsync(
        JsonObject projection,
        string? output,
        CancellationToken cancellationToken)
    {
        if (output is not null)
        {
            await WriteFileAsync(projection, output, cancellationToken);
            return;
        }

        await using var standardOutput = Console.OpenStandardOutput();
        await CatalogExport.WriteAsync(projection, standardOutput, cancellationToken);
    }

    // A file is replaced once complete: a failed export never leaves half a document.
    private static async Task WriteFileAsync(
        JsonObject projection,
        string output,
        CancellationToken cancellationToken)
    {
        var partial = output + PartialSuffix;
        try
        {
            await using (var file = File.Create(partial))
            {
                await CatalogExport.WriteAsync(projection, file, cancellationToken);
            }

            File.Move(partial, output, overwrite: true);
        }
        catch
        {
            // Exists first: Delete throws when the directory itself is missing.
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }

            throw;
        }
    }
}
