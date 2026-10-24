using System.Text.RegularExpressions;
using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Robots.Identification.Tables;

namespace LoDb.Infrastructure.Tests.Robots.Identification;

/// <summary>
/// nginx reports to the journal every request whose user agent its map
/// <c>$lodb_robot_agent</c> (docker/nginx/nginx.conf) takes for a robot's: a robot of the
/// catalog it would miss would never reach the API.
/// </summary>
public sealed partial class RobotAgentMapTests
{
    private const string MapStart = "map $http_user_agent $lodb_robot_agent {";

    [Fact]
    public void EveryTokenOfTheCatalogIsReported()
    {
        var patterns = MapPatterns();
        var tokens = RobotCatalog.Known.Concat(ToolAgents.All)
            .SelectMany(static profile => profile.Tokens);

        var missed = tokens.Where(token => !patterns.Any(pattern => pattern.IsMatch(token)));

        Assert.NotEmpty(patterns);
        Assert.Empty(missed);
    }

    [Fact]
    public void ABrowserIsNotReported()
    {
        var patterns = MapPatterns();

        Assert.DoesNotContain(
            patterns,
            static pattern => pattern.IsMatch(Support.UserAgents.Chrome)
                || pattern.IsMatch(Support.UserAgents.Firefox));
    }

    // The case-insensitive patterns of the map, as nginx reads them.
    private static List<Regex> MapPatterns()
    {
        var lines = File.ReadAllLines(
            Path.Combine(RepositoryRoot(), "docker", "nginx", "nginx.conf"));
        var start = Array.FindIndex(lines, line => line.Trim() == MapStart);
        return
        [
            .. lines.Skip(start + 1)
                .TakeWhile(static line => line.Trim() != "}")
                .Select(static line => PatternLine().Match(line))
                .Where(static match => match.Success)
                .Select(static match => new Regex(
                    match.Groups["pattern"].Value,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "LoDb.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root was not found.");
    }

    [GeneratedRegex("^\\s*\"~\\*(?<pattern>[^\"]+)\"\\s+1;")]
    private static partial Regex PatternLine();
}
