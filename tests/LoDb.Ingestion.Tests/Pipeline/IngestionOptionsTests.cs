using LoDb.Ingestion.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// The bounds of the on-demand ingestion: budgets of cold work per minute, ingestions waiting
/// for a slot and how long, refused at startup when out of bounds.
/// </summary>
public sealed class IngestionOptionsTests
{
    [Fact]
    public void DefaultsBoundTheColdWorkOfVisitors()
    {
        var options = Resolve(new());

        Assert.Equal(
            (30, 6, 1500, 32, TimeSpan.FromSeconds(3)),
            (options.CrawlerRequestsPerMinute, options.ColdDatasetsPerMinute,
                options.ColdImagesPerMinute, options.MaxWaitingIngestions, options.SlotWait));
    }

    [Theory]
    [InlineData("ColdDatasetsPerMinute", "-1")]
    [InlineData("ColdImagesPerMinute", "-1")]
    [InlineData("MaxWaitingIngestions", "-1")]
    [InlineData("SlotWait", "00:00:00")]
    [InlineData("SlotWait", "00:01:01")]
    [InlineData("SyncConcurrency", "0")]
    public void OutOfBoundsSettingIsRefused(string setting, string value)
    {
        var failure = Assert.Throws<OptionsValidationException>(
            () => Resolve(new() { [$"{IngestionOptions.SectionName}:{setting}"] = value }));

        Assert.Contains(
            failure.Failures,
            message => message.StartsWith(setting, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ColdDatasetsPerMinute", "0")]
    [InlineData("ColdImagesPerMinute", "0")]
    [InlineData("MaxWaitingIngestions", "0")]
    public void ZeroTurnsTheColdWorkOfVisitorsOff(string setting, string value) =>
        Assert.NotNull(Resolve(new() { [$"{IngestionOptions.SectionName}:{setting}"] = value }));

    private static IngestionOptions Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddLoDbIngestion(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<IngestionOptions>>().Value;
    }
}
