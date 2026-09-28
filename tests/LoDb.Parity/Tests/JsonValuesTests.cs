using System.Text.Json.Nodes;
using LoDb.Parity.Comparison;

namespace LoDb.Parity.Tests;

public sealed class JsonValuesTests
{
    [Theory]
    [InlineData("25.0", "25")]
    [InlineData("0.30", "0.3")]
    [InlineData("\"a\"", "\"a\"")]
    [InlineData("null", "null")]
    public void EqualValuesMatch(string legacy, string next) =>
        Assert.True(JsonValues.Equal(Parse(legacy), Parse(next)));

    [Theory]
    [InlineData("25", "26")]
    [InlineData("\"25\"", "25")]
    [InlineData("\"a \"", "\"a\"")]
    [InlineData("null", "\"\"")]
    public void DifferentValuesDoNot(string legacy, string next) =>
        Assert.False(JsonValues.Equal(Parse(legacy), Parse(next)));

    [Fact]
    public void AnImageRendersAsItsVerdict()
    {
        var image = Parse("""{"file":"A.png","status":"present","url":"/cdn/blobs/ab.png"}""");

        Assert.Equal("present /cdn/blobs/ab.png", JsonValues.Render(image));
    }

    [Fact]
    public void TextIsKeptReadableAndCut()
    {
        Assert.Equal("\"ف<é>\"", JsonValues.Render(Parse("\"ف<é>\"")));
        Assert.EndsWith("…", JsonValues.Render(JsonValue.Create(new string('x', 400))));
    }

    private static JsonNode? Parse(string json) => JsonNode.Parse(json);
}
