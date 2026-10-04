using LoDb.Api.Cli.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Tests.Ingestion;

/// <summary>
/// The options of <c>catalog export</c>: the (version, language), the output file, the
/// stored-only switch, and the host settings passed over.
/// </summary>
public sealed class CatalogExportArgumentsTests
{
    public static TheoryData<string[]> InvalidUses =>
    [
        Array.Empty<string>(),
        (string[])["--lang", "fr_FR"],
        (string[])["--version", "16.19.1"],
        (string[])["--version", "--lang", "fr_FR"],
        (string[])["--version", "v16.19.1", "--lang", "fr_FR"],
        (string[])["--version", "16.19.1", "--lang", "french"],
        (string[])["--version", "16.19.1", "--lang", "fr_FR", "--lang", "en_US"],
        (string[])["--version", "16.19.1", "--lang", "fr_FR", "--output="],
        (string[])["--version", "16.19.1", "--lang", "fr_FR", "--output"],
        (string[])["--version", "16.19.1", "--lang", "fr_FR", "--stored-only=true"],
        (string[])["--version", "16.19.1", "--lang", "fr_FR", "--pretty"],
        (string[])["16.19.1", "fr_FR"],
    ];

    [Fact]
    public void CatalogIsWrittenToTheStandardOutputByDefault()
    {
        var parsed = CatalogExportArguments.Parse(["--version", "16.19.1", "--lang", "fr_FR"]);

        Assert.Equal(PatchVersion.Parse("16.19.1"), parsed.Version);
        Assert.Equal(DdragonLanguage.Parse("fr_FR"), parsed.Language);
        Assert.Null(parsed.Output);
        Assert.False(parsed.StoredOnly);
    }

    [Fact]
    public void OptionsTakeTheirValueEitherWay()
    {
        var parsed = CatalogExportArguments.Parse(
        [
            "--stored-only",
            "--lang=en_US",
            "--output",
            "exports/16.19.1.json",
            "--version=16.19.1",
        ]);

        Assert.Equal(PatchVersion.Parse("16.19.1"), parsed.Version);
        Assert.Equal(DdragonLanguage.EnUs, parsed.Language);
        Assert.Equal("exports/16.19.1.json", parsed.Output);
        Assert.True(parsed.StoredOnly);
    }

    [Fact]
    public void HostSettingsArePassedOver()
    {
        var parsed = CatalogExportArguments.Parse(
        [
            "--ConnectionStrings:LoDb=Host=db;Database=lodb",
            "--version",
            "16.19.1",
            "--Logging:LogLevel:Default=None",
            "--lang",
            "ko_KR",
        ]);

        Assert.Equal(DdragonLanguage.Parse("ko_KR"), parsed.Language);
        Assert.Null(parsed.Output);
    }

    [Theory]
    [MemberData(nameof(InvalidUses))]
    public void InvalidUseIsAFormatError(string[] arguments)
    {
        Assert.Throws<FormatException>(() => CatalogExportArguments.Parse(arguments));
    }
}
