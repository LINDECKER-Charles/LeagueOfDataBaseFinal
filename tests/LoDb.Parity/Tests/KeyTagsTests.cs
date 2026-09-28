using System.Text.Json.Nodes;
using LoDb.Parity.Deviations;
using LoDb.Parity.Runs;

namespace LoDb.Parity.Tests;

public sealed class KeyTagsTests
{
    [Fact]
    public void ChampionFilesAreTaggedPortraitOrAbility()
    {
        var tags = new KeyTags();

        tags.Add(Projection("""
            {"champions":{"entries":[{"id":"Ahri","image":{"file":"Ahri.png"},
              "passive":{"image":{"file":"Ahri_P.png"}},
              "spells":[{"image":{"file":"AhriQ.png"}}]}]}}
            """));

        var champion = tags.Of("champion");
        Assert.Equal([DeviationTags.Portrait], champion["Ahri.png"]);
        Assert.Equal([DeviationTags.Ability], champion["Ahri_P.png"]);
        Assert.Equal([DeviationTags.Ability], champion["AhriQ.png"]);
    }

    [Fact]
    public void DebrisIsUnlistedInAnyLanguage()
    {
        const string Item = """
            {"items":{"entries":[{"image":{"file":"7050.png"},"listed":true}]}}
            """;
        var tags = new KeyTags();

        tags.Add(Projection(Item));
        tags.Add(Projection(Item.Replace("true", "false", StringComparison.Ordinal)));

        Assert.Equal([DeviationTags.Unlisted], tags.Of("item")["7050.png"]);
    }

    [Fact]
    public void IconsOfLegacyDetailsAreTaggedDetailed()
    {
        var tags = new KeyTags();

        tags.Add(Projection("""
            {"champions":{"entries":[{"id":"Ahri","spells":[{"image":{"file":"AhriQ.png"}}]}]}}
            """));
        tags.AddLegacyDetails(Projection("""
            {"champions":{"entries":[
              {"id":"Ahri","detail":true,"spells":[{"image":{"file":"AhriQ.png"}}]},
              {"id":"Annie","detail":false}]}}
            """));

        Assert.Equal(
            [DeviationTags.Ability, DeviationTags.Detailed],
            tags.Of("champion")["AhriQ.png"].Order(StringComparer.Ordinal));
    }

    private static JsonObject Projection(string json) => JsonNode.Parse(json)!.AsObject();
}
