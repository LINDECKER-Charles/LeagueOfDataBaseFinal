using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Catalog.Export;

/// <summary>
/// The canonical projection of a catalog: what the parity check compares with the export of
/// the legacy stack (L1.8).
/// </summary>
/// <remarks>
/// <para>
/// One document per (version, language): <c>version</c>, <c>language</c>, then
/// <c>champions</c>, <c>items</c>, <c>runes</c> and <c>summoners</c>, each with its
/// <c>contentLanguage</c> and its <c>entries</c> in the upstream order. Every entry carries
/// its id, name, canonical path and image; items and summoner spells their edition and twin,
/// classic ones included (the pickers leave them out); and the derived facts: resource token
/// and attack range of a champion, chroma labels, listing, tier and stat rows of an item.
/// </para>
/// <para>
/// An image is <c>{ file, status, url }</c>: <c>present</c> with its <c>/cdn/blobs/</c> URL,
/// <c>absent</c> for a placeholder for good, <c>pending</c> when never settled (the gaps of
/// a stored-only export). Enum values are camelCase, stat names snake_case.
/// </para>
/// </remarks>
public sealed class CatalogExport(IImageResolver resolver)
{
    // A file compared by the parity check, never embedded in a page: the letters of every
    // script and the characters HTML escapes are written as they are, as json_encode does,
    // and lines end the same on every system.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly byte[] FinalNewLine = "\n"u8.ToArray();

    /// <summary>
    /// The projection of a catalog, its images resolved as <paramref name="demand"/> allows.
    /// </summary>
    public async Task<JsonObject> ProjectAsync(
        CatalogSnapshot catalog,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(demand);
        var images = await resolver
            .ResolveAsync(catalog.Version, ImagesOf(catalog), demand, cancellationToken)
            .ConfigureAwait(false);
        return new ExportProjection(catalog, images).Build();
    }

    /// <summary>
    /// Writes a projection as indented UTF-8 JSON, lines ending with <c>\n</c>, characters
    /// unescaped wherever JSON allows it.
    /// </summary>
    public static async Task WriteAsync(
        JsonObject projection,
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(output);
        var writer = new Utf8JsonWriter(output, WriterOptions);
        await using (writer.ConfigureAwait(false))
        {
            projection.WriteTo(writer);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await output.WriteAsync(FinalNewLine, cancellationToken).ConfigureAwait(false);
    }

    private static List<DdragonImage> ImagesOf(CatalogSnapshot catalog) =>
    [
        .. catalog.Champions.Entries.SelectMany(VersionImages.Of)
            .Concat(catalog.Items.Entries.SelectMany(VersionImages.Of))
            .Concat(catalog.Runes.Entries.SelectMany(VersionImages.Of))
            .Concat(catalog.Summoners.Entries.SelectMany(VersionImages.Of)),
    ];
}
