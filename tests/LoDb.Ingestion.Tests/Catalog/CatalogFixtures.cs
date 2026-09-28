using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// Catalogs of the recording, built from the datasets the ingestion would store, as the
/// reader builds them: a language other than en_US over the en_US catalog.
/// </summary>
internal static class CatalogFixtures
{
    public static readonly DdragonLanguage English = DdragonLanguage.EnUs;
    public static readonly DdragonLanguage French = DdragonLanguage.Parse("fr_FR");

    public static PatchVersion Latest => DdragonFixtures.Latest;

    private static CancellationToken Token => ReplayHarness.Token;

    /// <summary>The catalog of <paramref name="language"/> for the latest version.</summary>
    public static Task<CatalogSnapshot> LatestAsync(DdragonLanguage language) =>
        ReadAsync(Latest, language);

    /// <summary>The catalog of a (version, language).</summary>
    public static async Task<CatalogSnapshot> ReadAsync(
        PatchVersion version,
        DdragonLanguage language)
    {
        using var harness = ReplayHarness.Create();
        var english = new CatalogSnapshot(
            version,
            English,
            await DatasetsAsync(harness, version, English),
            null);
        return language == English
            ? english
            : new CatalogSnapshot(
                version,
                language,
                await DatasetsAsync(harness, version, language),
                english);
    }

    /// <summary>The four datasets of a (version, language), as stored.</summary>
    public static async Task<VersionDatasets> DatasetsAsync(
        ReplayHarness harness,
        PatchVersion version,
        DdragonLanguage language)
    {
        var scope = new DatasetScope { Version = version, Language = language };
        var chromas = await harness.Datasets.ReadChromasAsync(version, Token);
        return new VersionDatasets
        {
            Champions = await harness.Datasets.ReadChampionsAsync(scope, chromas, Token),
            Items = await harness.Datasets.ReadItemsAsync(scope, Token),
            Runes = await harness.Datasets.ReadRunesAsync(scope, Token),
            Summoners = await harness.Datasets.ReadSummonersAsync(scope, Token),
        };
    }
}
