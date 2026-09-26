using System.Text.Json;
using LoDb.Ingestion.Egress;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// Fetches an upstream JSON file and reads it with its generated contract.
/// </summary>
internal static class UpstreamDocuments
{
    /// <returns>
    /// The document, or <see langword="null"/> when the upstream has none (403/404).
    /// </returns>
    /// <exception cref="UpstreamDocumentException">The body is not the expected JSON.</exception>
    /// <exception cref="Egress.Errors.EgressException">
    /// Any other failure: nothing to persist.
    /// </exception>
    public static async Task<TDocument?> ReadAsync<TDocument>(
        IEgressBatch batch,
        UpstreamDocument<TDocument> document,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        var outcome = await batch.FetchAsync(document.Url, cancellationToken).ConfigureAwait(false);
        if (outcome is not FetchOutcome.Present present)
        {
            return null;
        }

        await using (present.Content.ConfigureAwait(false))
        {
            return await DeserializeAsync(present.Content, document, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<TDocument> DeserializeAsync<TDocument>(
        Stream content,
        UpstreamDocument<TDocument> document,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        try
        {
            return await JsonSerializer
                .DeserializeAsync(content, document.Contract, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new UpstreamDocumentException("The document is null.", document.Url);
        }
        catch (JsonException exception)
        {
            throw new UpstreamDocumentException(
                "The document is not the expected JSON.", document.Url, exception);
        }
    }
}
