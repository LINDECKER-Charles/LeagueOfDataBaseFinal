using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon.Raw;
using LoDb.Ingestion.Ddragon.Raw.Champions;
using LoDb.Ingestion.Ddragon.Raw.CommunityDragon;
using LoDb.Ingestion.Ddragon.Raw.Items;
using LoDb.Ingestion.Ddragon.Raw.Runes;
using LoDb.Ingestion.Ddragon.Raw.Summoners;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// Every upstream file the ingestion reads, with the contract that reads it.
/// </summary>
internal static class RawDocuments
{
    private static DdragonJsonContext Contracts => DdragonJsonContext.Default;

    public static UpstreamDocument<List<string?>> Versions { get; } =
        new(DdragonUrls.Versions, Contracts.ListString);

    public static UpstreamDocument<List<string?>> Languages { get; } =
        new(DdragonUrls.Languages, Contracts.ListString);

    /// <summary>Every champion with its details: one request per language.</summary>
    public static UpstreamDocument<RawDataFile<RawChampion>> ChampionFull(
        PatchVersion version,
        DdragonLanguage language) =>
        new(DdragonUrls.Dataset(version, language, "championFull"),
            Contracts.RawDataFileRawChampion);

    /// <summary>Every champion, summary fields only.</summary>
    public static UpstreamDocument<RawDataFile<RawChampion>> ChampionSummaries(
        PatchVersion version,
        DdragonLanguage language) =>
        new(DdragonUrls.Dataset(version, language, "champion"), Contracts.RawDataFileRawChampion);

    /// <summary>One champion with its details.</summary>
    public static UpstreamDocument<RawDataFile<RawChampion>> ChampionDetail(
        PatchVersion version,
        DdragonLanguage language,
        string championId) =>
        new(DdragonUrls.ChampionDetail(version, language, championId),
            Contracts.RawDataFileRawChampion);

    public static UpstreamDocument<RawDataFile<RawItem>> Items(
        PatchVersion version,
        DdragonLanguage language) =>
        new(DdragonUrls.Dataset(version, language, "item"), Contracts.RawDataFileRawItem);

    public static UpstreamDocument<RawDataFile<RawSummonerSpell>> SummonerSpells(
        PatchVersion version,
        DdragonLanguage language) =>
        new(DdragonUrls.Dataset(version, language, "summoner"),
            Contracts.RawDataFileRawSummonerSpell);

    /// <summary>Rune paths: 403 before 7.22.1, and in en_US only on 7.22.1 (UP 1).</summary>
    public static UpstreamDocument<List<RawRuneTree?>> RuneTrees(
        PatchVersion version,
        DdragonLanguage language) =>
        new(DdragonUrls.Dataset(version, language, "runesReforged"), Contracts.ListRawRuneTree);

    /// <summary>CommunityDragon's skins of a patch, with their chromas.</summary>
    public static UpstreamDocument<Dictionary<string, RawCommunityDragonSkin?>> Skins(
        string patch) =>
        new(CommunityDragonUrls.Skins(patch),
            Contracts.DictionaryStringRawCommunityDragonSkin);
}
