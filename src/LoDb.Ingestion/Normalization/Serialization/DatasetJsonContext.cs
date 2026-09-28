using System.Text.Json.Serialization;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;

namespace LoDb.Ingestion.Normalization.Serialization;

/// <summary>
/// Generated contracts of the stored datasets.
/// </summary>
/// <remarks>
/// Metadata only: <see cref="StableOrder"/> rewrites the contracts, which the generated
/// fast path would bypass. Computed properties (<c>Edition</c>, <c>Charges</c>) are
/// read-only, hence not stored: they are derived again on read and cannot go stale.
/// </remarks>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    IgnoreReadOnlyProperties = true,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(DatasetDocument<ChampionDetail>))]
[JsonSerializable(typeof(DatasetDocument<Item>))]
[JsonSerializable(typeof(DatasetDocument<RuneTree>))]
[JsonSerializable(typeof(DatasetDocument<SummonerSpell>))]
internal sealed partial class DatasetJsonContext : JsonSerializerContext;
