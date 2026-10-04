using LoDb.Domain.Builds.Editing;
using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Tests.Builds.Editing;

/// <summary>
/// The items of the editor's steps, case by case as the legacy <c>stepList</c> spec states
/// them, and the gold they cost, ghosts counting nothing.
/// </summary>
public sealed class StepItemsTests
{
    // One step without items: a bare [] would read as no step at all.
    private static readonly string[] NoItems = [];

    [Fact]
    public void AStepHoldsEightItemsAtMost()
    {
        var steps = StepListTests.Steps(NoItems);
        for (var i = 0; i < BuildLimits.ItemsPerStepMax; i++)
        {
            steps = StepItems.AddItem(steps, 0, "2003");
        }

        Assert.Equal(BuildLimits.ItemsPerStepMax, steps[0].Items.Count);
        Assert.False(StepItems.CanAddItem(steps, 0));
        Assert.Same(steps, StepItems.AddItem(steps, 0, "2003"));
    }

    [Fact]
    public void AnItemMayBeBoughtTwice()
    {
        var steps = StepItems.AddItem(StepListTests.Steps(NoItems), 0, "2003");
        steps = StepItems.AddItem(steps, 0, "2003");

        Assert.Equal(["2003", "2003"], steps[0].Items);
    }

    [Fact]
    public void ABuildHoldsFortyItemsAtMost()
    {
        var steps = StepListTests.Steps([], [], [], [], [], []);
        for (var step = 0; step < 5; step++)
        {
            for (var i = 0; i < BuildLimits.ItemsPerStepMax; i++)
            {
                steps = StepItems.AddItem(steps, step, "1055");
            }
        }

        Assert.Equal(BuildLimits.TotalItemsMax, StepList.TotalItems(steps));
        Assert.False(StepItems.CanAddItem(steps, 5));
        Assert.Same(steps, StepItems.AddItem(steps, 5, "1055"));
    }

    [Fact]
    public void ItemsAreRemovedAndReorderedWithinAStep()
    {
        var steps = StepListTests.Steps(["a", "b", "c"]);

        Assert.Equal(["a", "c"], StepItems.RemoveItem(steps, new ItemLocation(0, 1))[0].Items);
        Assert.Equal(
            ["a", "c", "b"],
            StepItems.MoveItem(steps, new ItemLocation(0, 2), -1)[0].Items);
        Assert.Same(steps, StepItems.MoveItem(steps, new ItemLocation(0, 0), -1));
        Assert.Same(steps, StepItems.MoveItem(steps, new ItemLocation(1, 0), 1));
    }

    [Theory]
    [InlineData(0, 3, "b c a")]
    [InlineData(2, 0, "c a b")]
    [InlineData(1, 99, "a c b")]
    public void AnItemDropsAtAnInsertionPointOfItsStep(int from, int insert, string expected)
    {
        var steps = StepListTests.Steps(["a", "b", "c"]);

        var moved = StepItems.MoveItemToIndex(steps, new ItemLocation(0, from), insert);

        Assert.Equal(expected.Split(' '), moved[0].Items);
    }

    [Theory]
    [InlineData(1, "a z c")]
    [InlineData(-5, "z a c")]
    [InlineData(99, "a c z")]
    public void AnItemIsInsertedAtAClampedPosition(int index, string expected)
    {
        var steps = StepListTests.Steps(["a", "c"]);

        var inserted = StepItems.InsertItem(steps, new ItemLocation(0, index), "z");

        Assert.Equal(expected.Split(' '), inserted[0].Items);
    }

    [Fact]
    public void AnItemMovesToAnotherStepAtItsInsertionPoint()
    {
        var steps = StepListTests.Steps(["a", "b"], ["c"]);

        var moved = StepItems.TransferItem(steps, new ItemLocation(0, 1), new ItemLocation(1, 0));

        Assert.Equal(["a"], moved[0].Items);
        Assert.Equal(["b", "c"], moved[1].Items);
    }

    [Fact]
    public void AMoveWithinOneStepIsAReorder()
    {
        var steps = StepListTests.Steps(["a", "b", "c"]);

        var moved = StepItems.TransferItem(steps, new ItemLocation(0, 0), new ItemLocation(0, 3));

        Assert.Equal(["b", "c", "a"], moved[0].Items);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(5, 0)]
    [InlineData(1, 9)]
    public void AMoveIntoAFullStepOrFromNowhereIsRefused(int step, int index)
    {
        var steps = StepListTests.Steps([.. Enumerable.Repeat("x", 8)], ["y"]);

        Assert.Same(
            steps,
            StepItems.TransferItem(steps, new ItemLocation(step, index), new ItemLocation(0, 0)));
    }

    [Fact]
    public void AMoveMayEmptyItsStep()
    {
        var steps = StepListTests.Steps(["a"], ["b"]);

        var moved = StepItems.TransferItem(steps, new ItemLocation(0, 0), new ItemLocation(1, 1));

        Assert.Empty(moved[0].Items);
        Assert.Equal(["b", "a"], moved[1].Items);
    }

    [Fact]
    public void AStepCostsItsKnownItemsGhostsCountingNothing()
    {
        var step = new BuildStep { Label = "", Items = ["1055", "3006", "gone"] };

        Assert.Equal(1550, PurchaseGold.OfStep(step, GoldOf));
    }

    [Fact]
    public void ABuildCostsEveryStep()
    {
        var steps = StepListTests.Steps(["1055"], ["3006", "3006"]);

        Assert.Equal(2650, PurchaseGold.OfBuild(steps, GoldOf));
    }

    private static int? GoldOf(string itemId) => itemId switch
    {
        "1055" => 450,
        "3006" => 1100,
        _ => null,
    };
}
