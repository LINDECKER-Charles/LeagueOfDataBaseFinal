using System.Reflection;
using LoDb.Domain.Versions;

namespace LoDb.Domain.Tests.Versions;

/// <summary>
/// The offered versions: well-formed only, each once, newest first, and nothing claims a date
/// Data Dragon never publishes.
/// </summary>
public sealed class PatchVersionsTests
{
    [Fact]
    public void Up4LolpatchEntriesAreDropped()
    {
        var versions = PatchVersions.Normalize(
            ["16.14.1", "lolpatch_3.7", "0.151.2", "lolpatch_4.21", "16.13.1"]);

        Assert.Equal(["16.14.1", "16.13.1", "0.151.2"], versions.Select(version => version.Value));
    }

    [Fact]
    public void TheListIsSortedNewestFirstWhateverTheUpstreamOrder()
    {
        var versions = PatchVersions.Normalize(["9.24.2", "10.1.1", "15.1", "15.1.1", "10.10.1"]);

        Assert.Equal(
            ["15.1.1", "15.1", "10.10.1", "10.1.1", "9.24.2"],
            versions.Select(version => version.Value));
    }

    [Fact]
    public void DuplicatesAndBlanksAreDropped()
    {
        var versions = PatchVersions.Normalize(["16.14.1", null, "", "16.14.1"]);

        Assert.Equal("16.14.1", Assert.Single(versions).Value);
    }

    [Fact]
    public void Up13VersionsCarryNoDate()
    {
        Type[] dateTypes = [typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly)];

        var datedMembers = typeof(PatchVersion)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => dateTypes.Contains(
                Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType));

        Assert.Empty(datedMembers);
    }
}
