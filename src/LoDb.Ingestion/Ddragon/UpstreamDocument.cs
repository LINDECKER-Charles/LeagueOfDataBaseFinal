using System.Text.Json.Serialization.Metadata;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// An upstream JSON file and the generated contract that reads it.
/// </summary>
internal sealed record UpstreamDocument<TDocument>(Uri Url, JsonTypeInfo<TDocument> Contract)
    where TDocument : class;
