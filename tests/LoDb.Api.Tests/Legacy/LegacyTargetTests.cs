using LoDb.Api.Modules.Legacy.Targets;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// The pure choices of a target: locale and kept variant, version and short URL, the path
/// written. The recorded Data Dragon lists no regional variant, hence these cases here.
/// </summary>
public sealed class LegacyTargetTests
{
    private static readonly PatchVersion Latest = PatchVersion.Parse("16.19.1");
    private static readonly PatchVersion Previous = PatchVersion.Parse("16.18.1");

    private static readonly DdragonLanguage[] Listed =
        [.. new[] { "en_US", "en_GB", "fr_FR", "es_ES", "es_MX", "zh_CN", "zh_TW" }
            .Select(DdragonLanguage.Parse)];

    [Theory]
    [InlineData(null, "/en/champions")]
    [InlineData("", "/en/champions")]
    [InlineData("fr_FR", "/fr/champions")]
    [InlineData("en_GB", "/en/champions?lang=en_GB")]
    [InlineData("es_MX", "/es/champions?lang=es_MX")]
    [InlineData("zh_TW", "/zh-hant/champions")]
    [InlineData("pt_PT", "/pt/champions")]
    [InlineData("fr-FR", "/en/champions")]
    public void LocaleFollowsTheOldLanguageAndKeepsAListedVariant(string? lang, string target)
    {
        var locale = LegacyLocales.Choose(lang, Listed);

        Assert.Equal(target, LegacyTarget.Of(locale, null, "champions"));
    }

    [Theory]
    [InlineData(null, null, null, "16.19.1")]
    [InlineData("16.18.1", null, "16.18.1", "16.18.1")]
    [InlineData("16.19.1", "16.18.1", null, "16.19.1")]
    [InlineData(null, "16.18.1", "16.18.1", "16.18.1")]
    [InlineData(null, "16.19.1", null, "16.19.1")]
    [InlineData(null, "1.0.0", null, "16.19.1")]
    public void PathVersionWinsOverQueryAndTheLatestIsNeverPinned(
        string? pathVersion,
        string? queryVersion,
        string? pinned,
        string catalog)
    {
        var version = LegacyVersions.Choose(pathVersion, queryVersion, Versions(Latest));

        Assert.NotNull(version);
        Assert.Equal(pinned, version.Pinned?.Value);
        Assert.Equal(catalog, version.Catalog?.Value);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("16.19")]
    public void UnlistedPathVersionIsUnknown(string pathVersion) =>
        Assert.Null(LegacyVersions.Choose(pathVersion, null, Versions(Latest)));

    [Fact]
    public void BeforeAnyPromotionNothingIsShortened()
    {
        var pinned = LegacyVersions.Choose("16.19.1", null, Versions(latest: null));
        var unversioned = LegacyVersions.Choose(null, null, Versions(latest: null));

        Assert.Equal(Latest, pinned?.Pinned);
        Assert.NotNull(unversioned);
        Assert.Null(unversioned.Catalog);
    }

    [Fact]
    public void PinnedVersionPrecedesThePath()
    {
        var locale = LegacyLocales.Choose("fr_FR", Listed);

        Assert.Equal("/fr/16.18.1/items/1004-faerie-charm",
            LegacyTarget.Of(locale, Previous, "items/1004-faerie-charm"));
        Assert.Equal("/fr/", LegacyTarget.Of(locale, null, string.Empty));
    }

    private static CatalogVersions Versions(PatchVersion? latest) => new()
    {
        Latest = latest,
        Listed = [Latest, Previous],
        Ready = latest is null ? [] : [latest],
    };
}
