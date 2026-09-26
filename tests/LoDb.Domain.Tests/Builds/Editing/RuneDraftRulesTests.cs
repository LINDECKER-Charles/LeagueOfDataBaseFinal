using System.Globalization;
using LoDb.Domain.Builds.Editing;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Tests.Builds.Editing;

/// <summary>
/// The rune board, case by case as the legacy <c>runeRules</c> spec states it, the game
/// client's eviction of the oldest secondary pick included.
/// </summary>
public sealed class RuneDraftRulesTests
{
    private const int Precision = 8000;
    private const int Domination = 8100;
    private const int Sorcery = 8200;

    [Fact]
    public void ADraftStartsEmpty()
    {
        var draft = RuneDraftRules.Empty;

        Assert.Null(draft.PrimaryStyleId);
        Assert.Equal([null, null, null, null], draft.PrimaryPerks);
        Assert.Empty(draft.SecondaryPicks);
    }

    [Fact]
    public void APrimarySlotHoldsOnePerk()
    {
        var draft = RuneDraftRules.SelectPrimaryStyle(RuneDraftRules.Empty, Precision);
        draft = RuneDraftRules.SelectPrimaryPerk(draft, 1, 9101);
        draft = RuneDraftRules.SelectPrimaryPerk(draft, 1, 9111);

        Assert.Equal([null, 9111, null, null], draft.PrimaryPerks);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    public void AnOutOfRangeSlotIsIgnored(int slot)
    {
        var draft = RuneDraftRules.SelectPrimaryStyle(RuneDraftRules.Empty, Precision);

        Assert.Same(draft, RuneDraftRules.SelectPrimaryPerk(draft, slot, 1));
    }

    [Fact]
    public void SwitchingThePrimaryTreeEmptiesItsSlots()
    {
        var next = RuneDraftRules.SelectPrimaryStyle(FullDraft(), Sorcery);

        Assert.Equal([null, null, null, null], next.PrimaryPerks);
        Assert.Equal(Domination, next.SecondaryStyleId);
    }

    [Fact]
    public void ReselectingThePrimaryTreeKeepsEverything()
    {
        var draft = FullDraft();

        Assert.Same(draft, RuneDraftRules.SelectPrimaryStyle(draft, Precision));
    }

    [Fact]
    public void TakingTheSecondaryTreeAsPrimaryClearsTheSecondarySide()
    {
        var next = RuneDraftRules.SelectPrimaryStyle(FullDraft(), Domination);

        Assert.Null(next.SecondaryStyleId);
        Assert.Empty(next.SecondaryPicks);
    }

    [Fact]
    public void ThePrimaryTreeCannotBeSecondary()
    {
        var draft = RuneDraftRules.SelectPrimaryStyle(RuneDraftRules.Empty, Precision);

        Assert.Same(draft, RuneDraftRules.SelectSecondaryStyle(draft, Precision));
    }

    [Fact]
    public void SwitchingTheSecondaryTreeRestartsItsPicks()
    {
        var next = RuneDraftRules.SelectSecondaryStyle(FullDraft(), Sorcery);

        Assert.Equal(Sorcery, next.SecondaryStyleId);
        Assert.Empty(next.SecondaryPicks);
    }

    [Fact]
    public void AKeystoneRowPickIsNeverSecondary()
    {
        var draft = FullDraft();

        Assert.Same(draft, RuneDraftRules.SelectSecondaryPerk(draft, 0, 8112));
    }

    public static TheoryData<string> Sequences =>
    [
        "1:8126 1:8139 => 1:8139",
        "1:8126 2:8138 3:8106 => 2:8138 3:8106",
        "1:8126 2:8138 1:8139 3:8106 => 1:8139 3:8106",
    ];

    /// <summary>
    /// A pick replaces the one of its row and becomes the newest; past two picks the oldest
    /// leaves (FIFO).
    /// </summary>
    [Theory]
    [MemberData(nameof(Sequences))]
    public void SecondaryPicksEvictTheOldest(string sequence)
    {
        var parts = sequence.Split(" => ");
        var draft = RuneDraftRules.SelectSecondaryStyle(
            RuneDraftRules.SelectPrimaryStyle(RuneDraftRules.Empty, Precision),
            Domination);
        foreach (var pick in Picks(parts[0]))
        {
            draft = RuneDraftRules.SelectSecondaryPerk(draft, pick.SlotIndex, pick.PerkId);
        }

        Assert.Equal(Picks(parts[1]), draft.SecondaryPicks);
    }

    [Fact]
    public void ADraftIsCompleteOnceEverySlotIsFilled()
    {
        Assert.False(RuneDraftRules.IsComplete(RuneDraftRules.Empty));
        Assert.True(RuneDraftRules.IsComplete(FullDraft()));
    }

    [Fact]
    public void ACompleteDraftIsStoredAsTheEntityShape()
    {
        var runes = RuneDraftRules.ToRunes(FullDraft());

        Assert.Equal(Precision, runes.PrimaryStyleId);
        Assert.Equal([8005, 9101, 9104, 8014], runes.PrimarySelections);
        Assert.Equal(Domination, runes.SecondaryStyleId);
        Assert.Equal([8126, 8138], runes.SecondarySelections);
    }

    [Fact]
    public void APartialDraftInventsNothing()
    {
        var draft = RuneDraftRules.SelectPrimaryStyle(RuneDraftRules.Empty, Precision);
        draft = RuneDraftRules.SelectPrimaryPerk(draft, 2, 9104);

        var runes = RuneDraftRules.ToRunes(draft);

        Assert.Equal([9104], runes.PrimarySelections);
        Assert.Equal(RunePage.Unset, runes.SecondaryStyleId);
        Assert.Empty(runes.SecondarySelections);
    }

    [Fact]
    public void AStoredPageReanchorsItsSecondaryPicks()
    {
        var draft = RuneDraftRules.FromRunes(
            Stored(),
            static (_, perk) => perk switch { 8126 => 1, 8138 => 3, _ => null });

        Assert.Equal([8005, 9101, 9104, 8014], draft.PrimaryPerks);
        Assert.Equal(
            [new SecondaryPick(1, 8126), new SecondaryPick(3, 8138)],
            draft.SecondaryPicks);
        Assert.Equal(Domination, draft.SecondaryStyleId);
    }

    [Fact]
    public void UnknownPerksStayVisibleAsGhosts()
    {
        var draft = RuneDraftRules.FromRunes(Stored(), static (_, _) => null);

        Assert.All(
            draft.SecondaryPicks,
            static pick => Assert.Equal(RuneDraftRules.GhostSlot, pick.SlotIndex));
    }

    [Fact]
    public void UnsetStylesReadAsUnselected()
    {
        var draft = RuneDraftRules.FromRunes(RunePage.Blank, static (_, _) => null);

        Assert.Null(draft.PrimaryStyleId);
        Assert.Null(draft.SecondaryStyleId);
        Assert.Equal([null, null, null, null], draft.PrimaryPerks);
    }

    private static RuneDraft FullDraft()
    {
        var draft = RuneDraftRules.SelectPrimaryStyle(RuneDraftRules.Empty, Precision);
        int[] primary = [8005, 9101, 9104, 8014];
        for (var slot = 0; slot < primary.Length; slot++)
        {
            draft = RuneDraftRules.SelectPrimaryPerk(draft, slot, primary[slot]);
        }

        draft = RuneDraftRules.SelectSecondaryStyle(draft, Domination);
        draft = RuneDraftRules.SelectSecondaryPerk(draft, 1, 8126);
        return RuneDraftRules.SelectSecondaryPerk(draft, 2, 8138);
    }

    private static RunePage Stored() => new()
    {
        PrimaryStyleId = Precision,
        PrimarySelections = [8005, 9101, 9104, 8014],
        SecondaryStyleId = Domination,
        SecondarySelections = [8126, 8138],
    };

    // "1:8126 2:8138" lists picks as row:perk.
    private static SecondaryPick[] Picks(string picks) =>
        [.. picks.Split(' ').Select(static pick => pick.Split(':')).Select(static pair =>
            new SecondaryPick(Number(pair[0]), Number(pair[1])))];

    private static int Number(string text) => int.Parse(text, CultureInfo.InvariantCulture);
}
