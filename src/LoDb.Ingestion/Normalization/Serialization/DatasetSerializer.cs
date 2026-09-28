using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace LoDb.Ingestion.Normalization.Serialization;

/// <summary>
/// The stored form of a dataset: compact UTF-8 JSON, camelCase, properties and map keys in
/// ordinal order, null members omitted.
/// </summary>
/// <remarks>
/// The same document always yields the same bytes (<see cref="StableOrder"/>). Letters of
/// every script are written as is rather than as <c>\uXXXX</c> escapes: a Korean or Chinese
/// dataset stays readable and about half the size, while HTML-sensitive characters are still
/// escaped.
/// </remarks>
public static class DatasetSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>The bytes to hand to <c>IDatasetStore.WriteAsync</c>.</summary>
    public static byte[] Serialize<TEntry>(
        DatasetType<TEntry> type,
        DatasetDocument<TEntry> document)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.SerializeToUtf8Bytes(document, type.Contract);
    }

    /// <summary>Reads a stored dataset back.</summary>
    /// <exception cref="JsonException">The stream is not a dataset of this type.</exception>
    public static async Task<DatasetDocument<TEntry>> DeserializeAsync<TEntry>(
        DatasetType<TEntry> type,
        Stream json,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(type);
        return await JsonSerializer.DeserializeAsync(json, type.Contract, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new JsonException($"The {type.Name} dataset is null.");
    }

    internal static JsonTypeInfo<DatasetDocument<TEntry>> Contract<TEntry>() =>
        (JsonTypeInfo<DatasetDocument<TEntry>>)Options.GetTypeInfo(typeof(DatasetDocument<TEntry>));

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(DatasetJsonContext.Default.Options)
        {
            TypeInfoResolver = DatasetJsonContext.Default.WithAddedModifier(StableOrder.Apply),
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        };
        options.MakeReadOnly();
        return options;
    }
}
