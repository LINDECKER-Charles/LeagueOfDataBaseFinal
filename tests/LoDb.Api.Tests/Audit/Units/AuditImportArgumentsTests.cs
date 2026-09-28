using LoDb.Api.Cli.Audit;

namespace LoDb.Api.Tests.Audit.Units;

/// <summary>The options of <c>audit import</c>, host settings skipped.</summary>
public sealed class AuditImportArgumentsTests
{
    [Fact]
    public void SourcesKeepTheirOrder()
    {
        var options = AuditImportArguments.Parse(
            ["--source", "/local", "--ConnectionStrings:LoDb=Host=db", "--source=/archive"]);

        Assert.Equal(["/local", "/archive"], options.Sources);
        Assert.False(options.DryRun);
    }

    [Fact]
    public void DryRunIsASwitch()
    {
        Assert.True(AuditImportArguments.Parse(["--dry-run", "--source", "/local"]).DryRun);
    }

    // Arguments separated by one space; none for the empty line.
    [Theory]
    [InlineData("")]
    [InlineData("--dry-run")]
    [InlineData("--source")]
    [InlineData("--source --dry-run")]
    [InlineData("--source=")]
    [InlineData("--source /local --dry-run=yes")]
    [InlineData("--source /local --force")]
    [InlineData("/local")]
    public void InvalidUseIsRefused(string line) =>
        Assert.Throws<FormatException>(() => AuditImportArguments.Parse(
            line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
}
