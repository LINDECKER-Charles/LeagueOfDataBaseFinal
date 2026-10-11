using LoDb.Api.Cli.Analytics;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The options of <c>analytics import</c>: one source directory, the dry-run switch, and the
/// host settings passed over.
/// </summary>
public sealed class AnalyticsImportArgumentsTests
{
    private const string Source = "legacy/daily";

    public static TheoryData<string[]> InvalidUses =>
    [
        (string[])["--dry-run"],
        (string[])["--source"],
        (string[])["--source", "--dry-run"],
        (string[])["--source", Source, "--source", "other"],
        (string[])["--source", Source, "--days", "3"],
        (string[])["--source", Source, "--dry-run=yes"],
        (string[])[Source],
    ];

    [Fact]
    public void OptionsTakeTheirValueEitherWayPastTheHostSettings()
    {
        var parsed = AnalyticsImportArguments.Parse(
            ["--ConnectionStrings:LoDb=Host=db", $"--source={Source}", "--dry-run"]);

        Assert.Equal((Source, true), (parsed.Source, parsed.DryRun));
    }

    [Theory]
    [MemberData(nameof(InvalidUses))]
    public void InvalidUseIsAFormatError(string[] arguments)
    {
        Assert.Throws<FormatException>(() => AnalyticsImportArguments.Parse(arguments));
    }
}
