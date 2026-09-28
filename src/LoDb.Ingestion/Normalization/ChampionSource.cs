using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Ddragon.Raw;
using LoDb.Ingestion.Ddragon.Raw.Champions;
using LoDb.Ingestion.Egress;

namespace LoDb.Ingestion.Normalization;

/// <summary>
/// The raw champions of a (version, language), whichever files the version ships (UP 3).
/// </summary>
/// <remarks>
/// <c>championFull.json</c> holds every champion's details in one request. Versions without it
/// (0.x, 3.6.14) have <c>champion.json</c> plus one file per champion, some of them 403: the
/// summary then stands in for the missing details.
/// </remarks>
internal sealed class ChampionSource(PatchVersion version, int concurrency)
{
    /// <returns>The champions, or <see langword="null"/> when the language ships none.</returns>
    public async Task<IReadOnlyList<RawChampion?>?> ReadAsync(
        IEgressBatch batch,
        DdragonLanguage language,
        CancellationToken cancellationToken)
    {
        var full = await UpstreamDocuments
            .ReadAsync(batch, RawDocuments.ChampionFull(version, language), cancellationToken)
            .ConfigureAwait(false);
        if (full is not null)
        {
            return Entries(full);
        }

        var summaries = await UpstreamDocuments
            .ReadAsync(batch, RawDocuments.ChampionSummaries(version, language), cancellationToken)
            .ConfigureAwait(false);
        if (summaries is null)
        {
            return null;
        }

        var read = new DetailRead(batch, language);
        return await ReadDetailsAsync(read, Entries(summaries), cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<RawChampion?> Entries(RawDataFile<RawChampion> file) =>
        [.. (file.Data ?? []).Values];

    // Details land at their summary's index: the upstream order survives the parallelism.
    private async Task<IReadOnlyList<RawChampion?>> ReadDetailsAsync(
        DetailRead read,
        List<RawChampion?> summaries,
        CancellationToken cancellationToken)
    {
        var details = new RawChampion?[summaries.Count];
        var parallelism = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(
            Enumerable.Range(0, summaries.Count),
            parallelism,
            async (index, token) =>
                details[index] = await ReadDetailAsync(read, summaries[index], token)
                    .ConfigureAwait(false))
            .ConfigureAwait(false);
        return details;
    }

    private async Task<RawChampion?> ReadDetailAsync(
        DetailRead read,
        RawChampion? summary,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(summary?.Id))
        {
            return summary;
        }

        var document = RawDocuments.ChampionDetail(version, read.Language, summary.Id);
        var file = await UpstreamDocuments
            .ReadAsync(read.Batch, document, cancellationToken)
            .ConfigureAwait(false);
        return file?.Data?.Values.FirstOrDefault(static detail => detail is not null) ?? summary;
    }

    private sealed record DetailRead(IEgressBatch Batch, DdragonLanguage Language);
}
