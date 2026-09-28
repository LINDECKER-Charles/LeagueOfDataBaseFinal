using LoDb.Domain.Paths;
using LoDb.Domain.Versions;

namespace LoDb.Domain.Tests.Paths;

/// <summary>
/// Dataset icons live under their version, rune icons do not; every path is relative to the
/// CDN root the caller configures.
/// </summary>
public sealed class DdragonImagePathTests
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.14.1");

    [Fact]
    public void DatasetIconsLiveUnderTheirVersion()
    {
        Assert.Equal(
            "16.14.1/img/champion/Ahri.png",
            DdragonImagePath.Champion(Version, "Ahri.png"));
        Assert.Equal(
            "16.14.1/img/passive/Ahri_SoulEater2.png",
            DdragonImagePath.Passive(Version, "Ahri_SoulEater2.png"));
        Assert.Equal("16.14.1/img/spell/AhriQ.png", DdragonImagePath.Spell(Version, "AhriQ.png"));
        Assert.Equal("16.14.1/img/item/3078.png", DdragonImagePath.Item(Version, "3078.png"));
    }

    [Fact]
    public void Up5RuneIconsAreUnversioned()
    {
        Assert.Equal(
            "img/perk-images/Styles/7201_Precision.png",
            DdragonImagePath.RuneIcon("perk-images/Styles/7201_Precision.png"));
    }

    [Fact]
    public void Up5DeadDdsRuneIconsAreKeptVerbatim()
    {
        // 7.22 to 8.7 ship .dds icons that answer 403: their absence is recorded, never
        // papered over by a rewritten path.
        Assert.Equal(
            "img/perk-images/Styles/7200_Domination.dds",
            DdragonImagePath.RuneIcon("perk-images/Styles/7200_Domination.dds"));
    }
}
