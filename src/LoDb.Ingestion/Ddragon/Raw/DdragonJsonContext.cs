using System.Text.Json.Serialization;
using LoDb.Ingestion.Ddragon.Raw.Champions;
using LoDb.Ingestion.Ddragon.Raw.CommunityDragon;
using LoDb.Ingestion.Ddragon.Raw.Items;
using LoDb.Ingestion.Ddragon.Raw.Runes;
using LoDb.Ingestion.Ddragon.Raw.Summoners;

namespace LoDb.Ingestion.Ddragon.Raw;

/// <summary>
/// Reading contract of the upstream files, generated at build time.
/// </summary>
/// <remarks>
/// Upstream names are camelCase or lowercase ("allytips", "maxammo"), hence the
/// case-insensitive matching; numbers written as strings are accepted. Unknown fields are
/// skipped: the files carry much more than the ingestion reads.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(List<string?>))]
[JsonSerializable(typeof(RawDataFile<RawChampion>))]
[JsonSerializable(typeof(RawDataFile<RawItem>))]
[JsonSerializable(typeof(RawDataFile<RawSummonerSpell>))]
[JsonSerializable(typeof(List<RawRuneTree?>))]
[JsonSerializable(typeof(Dictionary<string, RawCommunityDragonSkin?>))]
internal sealed partial class DdragonJsonContext : JsonSerializerContext;
