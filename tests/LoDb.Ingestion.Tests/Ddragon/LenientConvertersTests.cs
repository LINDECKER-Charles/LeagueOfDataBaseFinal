using System.Text;
using System.Text.Json;
using LoDb.Ingestion.Ddragon.Raw;
using LoDb.Ingestion.Ddragon.Raw.Summoners;

namespace LoDb.Ingestion.Tests.Ddragon;

/// <summary>
/// The lenient converters read odd values of a file streamed in several blocks, as a real
/// <c>summoner.json</c> over the serializer's 16 KB buffer is.
/// </summary>
/// <remarks>
/// The recorded fixtures are trimmed under that buffer: they never reached the second block.
/// </remarks>
public sealed class LenientConvertersTests
{
    // Over the serializer's default buffer, so that the file comes in several blocks.
    private const int Padding = 40_000;

    // Before 4.x, range is the word "self" (0.154.3 el_GR, 5.2.1 th_TH, 29 KB and 22 KB).
    [Fact]
    public async Task WordRangeOfALargeFileReadsAsEmpty()
    {
        var spells = await ReadAsync("""
            "SummonerBarrier": {"id": "SummonerBarrier", "range": "self", "cooldown": [210]}
            """);

        var barrier = spells["SummonerBarrier"]!;
        Assert.Empty(barrier.Range!);
        Assert.Equal([210d], barrier.Cooldown);
        Assert.Equal("SummonerFlash", spells["SummonerFlash"]!.Id);
    }

    [Fact]
    public async Task OddValuesOfALargeFileReadAsMissing()
    {
        var spells = await ReadAsync("""
            "SummonerHeal": {"id": "SummonerHeal", "cost": [0, {"x": 1}, "5"], "maxammo": [1]}
            """);

        var heal = spells["SummonerHeal"]!;
        Assert.Equal([0d, 5d], heal.Cost);
        Assert.Null(heal.MaxAmmo);
    }

    // The odd spell comes first, in the first block; a long one after pushes the file over it.
    private static async Task<Dictionary<string, RawSummonerSpell?>> ReadAsync(string oddSpell)
    {
        var flash = $$"""
            "SummonerFlash": {"id": "SummonerFlash", "description": "{{new string('x', Padding)}}"}
            """;
        var json = $$$"""{"data": { {{{oddSpell}}}, {{{flash}}} }}""";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var file = await JsonSerializer.DeserializeAsync(
            stream,
            DdragonJsonContext.Default.RawDataFileRawSummonerSpell,
            TestContext.Current.CancellationToken);
        return file!.Data!;
    }
}
