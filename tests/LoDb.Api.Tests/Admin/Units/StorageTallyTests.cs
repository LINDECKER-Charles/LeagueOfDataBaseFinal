using LoDb.Api.Modules.Admin.Storage;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>The counts of the storage report, and the order of the versions it covers.</summary>
public sealed class StorageTallyTests
{
    [Fact]
    public void RowsComeHeaviestFirstWithTheirShare()
    {
        var tally = new StorageTally();
        tally.Add("png", 10);
        tally.Add("webp", 30);
        tally.Add("png", 20);
        tally.Add("jpg", 40);

        var rows = tally.Rows();

        Assert.Equal(
            [("jpg", 1L, 40L, 40.0), ("png", 2L, 30L, 30.0), ("webp", 1L, 30L, 30.0)],
            rows.Select(static row => (row.Name, row.Objects, row.Bytes, row.Pct)));
        Assert.Equal((4L, 100L), (tally.Objects, tally.Bytes));
    }

    [Fact]
    public void AnEmptyTallyHasNoShare()
    {
        var tally = new StorageTally();
        tally.Add("empty", 0);

        Assert.Equal(0.0, Assert.Single(tally.Rows()).Pct);
    }

    [Fact]
    public void VersionsSortByTheirNumbers()
    {
        var coverage = new DatasetCoverage();
        foreach (var version in new[] { "15.9.1", "16.1.1", "15.18.1", "lolpatch_7.20" })
        {
            coverage.Add(version, "fr_FR", "champion");
        }

        Assert.Equal(
            ["16.1.1", "15.18.1", "15.9.1", "lolpatch_7.20"],
            coverage.Rows().Select(static row => row.Version));
    }
}
