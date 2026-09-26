using System.Text.Json.Nodes;
using LoDb.Parity.Deviations;
using LoDb.Parity.Projections;

namespace LoDb.Parity.Tests;

public sealed class ProjectionComparerTests
{
    [Fact]
    public void EqualProjectionsGiveNothing()
    {
        const string Items = """
            {"contentLanguage":"fr_FR","entries":[{"id":"1001","name":"Bottes","listed":true}]}
            """;

        Assert.Empty(Compare(Projection(items: Items), Projection(items: Items)));
    }

    [Fact]
    public void AContentLanguageDeviationIsTaggedFallback()
    {
        var found = Compare(
            Projection(items: """{"contentLanguage":"en_US","entries":[]}"""),
            Projection(items: """{"contentLanguage":"fr_FR","entries":[]}"""));

        var deviation = Assert.Single(found);
        Assert.Equal(DeviationKind.ContentLanguage, deviation.Kind);
        Assert.Equal(("en_US", "fr_FR"), (deviation.Legacy, deviation.Next));
        Assert.Contains(DeviationTags.Fallback, deviation.Tags);
    }

    [Fact]
    public void AnEntryOnOneSideIsReportedOnItsId()
    {
        var found = Compare(
            Projection(items: Entries("""{"id":"1001","listed":false}""")),
            Projection(items: Entries("""{"id":"1001","listed":false}""", """{"id":"2008"}""")));

        var deviation = Assert.Single(found);
        Assert.Equal((DeviationKind.OnlyInNext, "items", "2008", ""),
            (deviation.Kind, deviation.Site.Resource, deviation.Site.Entry, deviation.Field));
        Assert.Null(deviation.Legacy);
    }

    [Fact]
    public void EntryOrderIsComparedOnce()
    {
        var found = Compare(
            Projection(items: Entries("""{"id":"1"}""", """{"id":"2"}""")),
            Projection(items: Entries("""{"id":"2"}""", """{"id":"1"}""")));

        var deviation = Assert.Single(found);
        Assert.Equal((DeviationKind.Order, "entries"), (deviation.Kind, deviation.Field));
    }

    [Fact]
    public void PathAndTheDetailMarkerAreNotCompared()
    {
        var found = Compare(
            Projection(champions: Entries("""{"id":"Ahri","detail":true}""")),
            Projection(champions: Entries("""{"id":"Ahri","path":"champions/Ahri"}""")));

        Assert.Empty(found);
    }

    [Fact]
    public void DetailFieldsAreOnlyComparedWhenTheLegacyStackHoldsTheDetail()
    {
        const string Next = """{"id":"Ahri","name":"Ahri","spells":[{"id":"AhriQ"}]}""";

        Assert.Empty(Compare(
            Projection(champions: Entries("""{"id":"Ahri","name":"Ahri","detail":false}""")),
            Projection(champions: Entries(Next))));
        var deviation = Assert.Single(Compare(
            Projection(champions: Entries("""{"id":"Ahri","name":"Ahri","detail":true}""")),
            Projection(champions: Entries(Next))));
        Assert.Equal(("spells", "Ahri"), (deviation.Field, deviation.Site.Entry));
        Assert.Contains(DeviationTags.Detailed, deviation.Tags);
    }

    [Fact]
    public void EntryFactsBecomeTags()
    {
        const string Legacy = """{"id":"772139","name":"","listed":false,"edition":"classic"}""";

        var deviation = Assert.Single(Compare(
            Projection(items: Entries(Legacy)),
            Projection(items: Entries(Legacy.Replace("\"\"", "\"x\"", StringComparison.Ordinal)))));

        Assert.Equal(
            [DeviationTags.Classic, DeviationTags.Unlisted],
            deviation.Tags.Order(StringComparer.Ordinal));
    }

    private static IReadOnlyList<Deviation> Compare(JsonObject legacy, JsonObject next) =>
        ProjectionComparer.Compare(new ProjectionPair
        {
            Version = "16.19.1",
            Language = "fr_FR",
            Legacy = legacy,
            Next = next,
        });

    private static string Entries(params string[] entries) =>
        $$"""{"contentLanguage":"fr_FR","entries":[{{string.Join(',', entries)}}]}""";

    private static JsonObject Projection(string? champions = null, string? items = null)
    {
        const string Empty = """{"contentLanguage":"fr_FR","entries":[]}""";
        return JsonNode.Parse($$"""
            {
              "version": "16.19.1",
              "language": "fr_FR",
              "champions": {{champions ?? Empty}},
              "items": {{items ?? Empty}},
              "runes": {{Empty}},
              "summoners": {{Empty}}
            }
            """)!.AsObject();
    }
}
