namespace LoDb.Api.Tests.Catalog.Metrics;

/// <summary>
/// The catalog's series as Prometheus reads them, under the names the alerts rely on, and
/// without a version, a language or an address in a label.
/// </summary>
[Collection(CatalogMetricsGroup.Name)]
public sealed class CatalogMetricsTests(CatalogApiFixture api)
{
    private static readonly (string Name, string Labels)[] Series =
    [
        ("lodb_catalog_cache_lookups_total{", "outcome=\"miss\""),
        ("lodb_catalog_cache_lookups_total{", "outcome=\"hit\""),
        ("lodb_catalog_load_duration_seconds_bucket{", "outcome=\"built\""),
        ("lodb_catalog_loads_in_flight", string.Empty),
        ("lodb_catalog_cold_admissions_total{", "partition=\"internal\",result=\"admitted\""),
        ("lodb_ingestion_sync_waiting", string.Empty),
    ];

    [Fact]
    public async Task ColdCatalogIsMeasured()
    {
        using var cold = await api.GetAsync("/api/catalog/16.18.1/zh_CN/summoners");
        using var warm = await api.GetAsync("/api/catalog/16.18.1/zh_CN/summoners");

        var lines = (await api.ScrapeAsync()).Split('\n');

        Assert.All(Series, series => Assert.Contains(lines, line =>
            line.StartsWith(series.Name, StringComparison.Ordinal)
            && line.Contains(series.Labels, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task NoLabelNamesAVersionOrALanguage()
    {
        using var cold = await api.GetAsync("/api/catalog/16.18.1/ar_AE/summoners");

        var catalogLines = (await api.ScrapeAsync()).Split('\n')
            .Where(static line => line.StartsWith("lodb_catalog_", StringComparison.Ordinal));

        Assert.All(catalogLines, static line =>
        {
            Assert.DoesNotContain("16.18.1", line, StringComparison.Ordinal);
            Assert.DoesNotContain("ar_AE", line, StringComparison.Ordinal);
        });
    }
}
