using System.Text.Json;
using LoDb.Domain.Catalog.Modes;

namespace LoDb.Domain.Tests.Catalog.Modes;

/// <summary>
/// The build modes: persisted codes and Data Dragon map ids are both frozen contracts.
/// </summary>
public sealed class GameModesTests
{
    [Fact]
    public void Up11BuildModesPlayOnTheirDdragonMaps()
    {
        Assert.Equal(GameMap.SummonersRift, GameModes.MapOf(GameMode.SummonersRift));
        Assert.Equal(GameMap.HowlingAbyss, GameModes.MapOf(GameMode.Aram));
        Assert.Equal(GameMap.NexusBlitz, GameModes.MapOf(GameMode.NexusBlitz));
        Assert.Equal(GameMap.Arena, GameModes.MapOf(GameMode.Arena));
        Assert.Equal([11, 12, 21, 30], GameModes.All.Select(mode => (int)GameModes.MapOf(mode)));
    }

    [Fact]
    public void ThePersistedCodesAreStable()
    {
        Assert.Equal(["sr", "aram", "nexus_blitz", "arena"], GameModes.All.Select(GameModes.Code));
        Assert.Equal(GameMode.SummonersRift, GameModes.Default);
    }

    [Fact]
    public void NoNamingPolicyWritesThePersistedCodes()
    {
        var snakeCase = JsonNamingPolicy.SnakeCaseLower.ConvertName(nameof(GameMode.SummonersRift));

        Assert.NotEqual(GameModes.Code(GameMode.SummonersRift), snakeCase);
    }

    [Theory]
    [InlineData(null, GameMode.SummonersRift)]
    [InlineData("", GameMode.SummonersRift)]
    [InlineData("  ", GameMode.SummonersRift)]
    [InlineData("aram", GameMode.Aram)]
    [InlineData(" aram ", GameMode.Aram)]
    [InlineData("nexus_blitz", GameMode.NexusBlitz)]
    public void ABlankRequestMeansTheDefaultMode(string? requested, GameMode expected)
    {
        Assert.Equal(expected, GameModes.Resolve(requested));
    }

    [Theory]
    [InlineData("urf")]
    [InlineData("ARAM")]
    [InlineData("SummonersRift")]
    public void AnUnknownModeIsRejected(string requested)
    {
        Assert.Null(GameModes.Resolve(requested));
        Assert.False(GameModes.TryParse(requested, out _));
    }
}
