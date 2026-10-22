using LoDb.Api.Cli.Catalog;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// <c>catalog states build</c> takes two switches and lets the host's settings through.
/// </summary>
public sealed class CatalogStatesBuildArgumentsTests
{
    [Fact]
    public void SwitchesAreReadAndHostSettingsSkipped()
    {
        var options = CatalogStatesBuildArguments.Parse(
            ["--rebuild", "--ConnectionStrings:LoDb=Host=db", "--ingest-missing"]);

        Assert.Equal((true, true), (options.ShouldRebuild, options.ShouldIngestMissing));
    }

    [Fact]
    public void NoSwitchCatchesUpOnly()
    {
        var options = CatalogStatesBuildArguments.Parse([]);

        Assert.Equal((false, false), (options.ShouldRebuild, options.ShouldIngestMissing));
    }

    [Theory]
    [InlineData("--rebuild=true")]
    [InlineData("--unknown")]
    [InlineData("rebuild")]
    [InlineData("--rebuild", "--rebuild")]
    public void InvalidUseIsRefused(params string[] arguments)
    {
        Assert.Throws<FormatException>(() => CatalogStatesBuildArguments.Parse(arguments));
    }
}
