using System.Text.Json.Nodes;
using LoDb.Parity.Comparison;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Tests;

public sealed class NodeComparerTests
{
    private static readonly DeviationSite Site = new()
    {
        Version = "16.19.1",
        Language = "fr_FR",
        Resource = "champions",
        Entry = "Ahri",
    };

    [Fact]
    public void EqualTreesGiveNothing()
    {
        const string Tree = """{"a":1,"b":[{"id":"x","n":2.0}],"c":[[1,2],[3]]}""";

        Assert.Empty(Compare(Tree, Tree.Replace("2.0", "2", StringComparison.Ordinal)));
    }

    [Fact]
    public void AValueIsReportedAtItsPath()
    {
        var found = Compare("""{"passive":{"name":"A"}}""", """{"passive":{"name":"B"}}""");

        var deviation = Assert.Single(found);
        Assert.Equal((DeviationKind.Value, "passive.name"), (deviation.Kind, deviation.Field));
        Assert.Equal(("\"A\"", "\"B\""), (deviation.Legacy, deviation.Next));
        Assert.Equal(Site, deviation.Site);
    }

    [Fact]
    public void KeyedElementsPairByIdWhateverTheirPosition()
    {
        var found = Compare(
            """{"spells":[{"id":"Q","n":1},{"id":"W","n":2},{"id":"E","n":3}]}""",
            """{"spells":[{"id":"W","n":2},{"id":"Q","n":9},{"id":"R","n":4}]}""");

        Assert.Equal(
            [
                (DeviationKind.OnlyInLegacy, "spells[E]"),
                (DeviationKind.OnlyInNext, "spells[R]"),
                (DeviationKind.Order, "spells"),
                (DeviationKind.Value, "spells[Q].n"),
            ],
            found.Select(static d => (d.Kind, d.Field)));
    }

    [Fact]
    public void StatRowsPairByStat()
    {
        var found = Compare(
            """{"stats":[{"stat":"armor","value":20},{"stat":"health","value":150}]}""",
            """{"stats":[{"stat":"armor","value":20},{"stat":"health","value":200}]}""");

        Assert.Equal("stats[health].value", Assert.Single(found).Field);
    }

    [Fact]
    public void RepeatedKeysPairByOccurrence()
    {
        var found = Compare(
            """{"skins":[{"id":"","name":"a"},{"id":"","name":"b"}]}""",
            """{"skins":[{"id":"","name":"a"},{"id":"","name":"c"}]}""");

        Assert.Equal("skins[#2].name", Assert.Single(found).Field);
    }

    [Fact]
    public void UnkeyedListsCompareByPositionThenAsAWhole()
    {
        Assert.Equal("slots[1][0]", Assert.Single(Compare("""{"slots":[[1],[2]]}""",
            """{"slots":[[1],[3]]}""")).Field);
        Assert.Equal("slots", Assert.Single(Compare("""{"slots":[[1]]}""",
            """{"slots":[[1],[2]]}""")).Field);
    }

    [Fact]
    public void AnImageDiffersByFileThenByVerdict()
    {
        var found = Compare(
            """{"image":{"file":"A.png","status":"pending","url":null}}""",
            """{"image":{"file":"B.png","status":"present","url":"/cdn/blobs/ab.png"}}""");

        Assert.Equal(
            [
                ("image.file", "\"A.png\"", "\"B.png\""),
                ("image", "pending", "present /cdn/blobs/ab.png"),
            ],
            found.Select(static d => (d.Field, d.Legacy, d.Next)));
    }

    [Fact]
    public void AFieldMissingOnOneSideComparesAsNull()
    {
        var deviation = Assert.Single(Compare("""{"tier":"epic"}""", "{}"));

        Assert.Equal(("\"epic\"", "null"), (deviation.Legacy, deviation.Next));
    }

    private static IReadOnlyList<Deviation> Compare(string legacy, string next)
    {
        var comparer = new NodeComparer(Site, new HashSet<string>());
        comparer.Compare(JsonNode.Parse(legacy), JsonNode.Parse(next), "");
        return comparer.Found;
    }
}
