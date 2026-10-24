namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>The crawlers of web archives.</summary>
internal static class ArchiveRobots
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Archive,
        [
            new("archive-org", "Internet Archive", "Internet Archive",
                "archive.org_bot|ia_archiver|heritrix|archive.org"),
            new("arquivo", "Arquivo.pt", "Arquivo.pt", "arquivo-web-crawler"),
        ]);
}
