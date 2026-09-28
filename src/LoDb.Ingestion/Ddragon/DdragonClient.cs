using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Egress;

namespace LoDb.Ingestion.Ddragon;

/// <inheritdoc />
internal sealed class DdragonClient(IEgressFetcher fetcher) : IDdragonClient
{
    public async Task<IReadOnlyList<PatchVersion>> GetVersionsAsync(
        CancellationToken cancellationToken)
    {
        var entries = await ReadRequiredAsync(RawDocuments.Versions, cancellationToken)
            .ConfigureAwait(false);
        return PatchVersions.Normalize(entries);
    }

    public async Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
        CancellationToken cancellationToken)
    {
        var entries = await ReadRequiredAsync(RawDocuments.Languages, cancellationToken)
            .ConfigureAwait(false);
        var languages = new List<DdragonLanguage>();
        foreach (var entry in entries)
        {
            if (DdragonLanguage.TryParse(entry, out var language) && !languages.Contains(language))
            {
                languages.Add(language);
            }
        }

        return languages;
    }

    // Both lists always exist: their absence is an upstream fault, never an empty answer.
    private async Task<List<string?>> ReadRequiredAsync(
        UpstreamDocument<List<string?>> document,
        CancellationToken cancellationToken)
    {
        using var batch = fetcher.OpenBatch();
        return await UpstreamDocuments.ReadAsync(batch, document, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new UpstreamDocumentException("The list is absent.", document.Url);
    }
}
