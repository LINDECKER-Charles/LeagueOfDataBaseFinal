using LoDb.Api.Cli.Ingest;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Tests.Ingestion;

/// <summary>
/// The options of <c>ingest</c>: one version or the newest ones, the languages, and the
/// host settings passed over.
/// </summary>
public sealed class IngestArgumentsTests
{
    public static TheoryData<string[]> InvalidUses =>
    [
        Array.Empty<string>(),
        (string[])["--languages", "en_US"],
        (string[])["--version", "16.19.1", "--latest", "1"],
        (string[])["--version"],
        (string[])["--version", "--force"],
        (string[])["--version", "v16.19.1"],
        (string[])["--version", "16.19.1", "--version", "16.18.1"],
        (string[])["--latest", "0"],
        (string[])["--latest", "51"],
        (string[])["--latest", "-1"],
        (string[])["--latest", "one"],
        (string[])["--version", "16.19.1", "--languages", "french"],
        (string[])["--version", "16.19.1", "--languages", ","],
        (string[])["--version", "16.19.1", "--force=true"],
        (string[])["--version", "16.19.1", "--everything"],
        (string[])["16.19.1"],
    ];

    [Fact]
    public void NamedVersionInEveryLanguage()
    {
        var parsed = IngestArguments.Parse(["--version", "16.19.1"]);

        Assert.Equal(PatchVersion.Parse("16.19.1"), parsed.Version);
        Assert.Equal(0, parsed.Latest);
        Assert.Null(parsed.Languages);
        Assert.False(parsed.Force);
    }

    [Fact]
    public void OptionsTakeTheirValueEitherWay()
    {
        var parsed = IngestArguments.Parse(
            ["--force", "--version=16.19.1", "--languages", "en_US, fr_FR,en_US"]);

        Assert.Equal(PatchVersion.Parse("16.19.1"), parsed.Version);
        Assert.Equal(
            [DdragonLanguage.EnUs, DdragonLanguage.Parse("fr_FR")],
            parsed.Languages);
        Assert.True(parsed.Force);
    }

    [Fact]
    public void HostSettingsArePassedOver()
    {
        var parsed = IngestArguments.Parse(
        [
            "--ConnectionStrings:LoDb=Host=db;Database=lodb",
            "--latest",
            "3",
            "--languages=all",
            "--Logging:LogLevel:Default=Warning",
        ]);

        Assert.Null(parsed.Version);
        Assert.Equal(3, parsed.Latest);
        Assert.Null(parsed.Languages);
    }

    [Theory]
    [MemberData(nameof(InvalidUses))]
    public void InvalidUseIsAFormatError(string[] arguments)
    {
        Assert.Throws<FormatException>(() => IngestArguments.Parse(arguments));
    }
}
