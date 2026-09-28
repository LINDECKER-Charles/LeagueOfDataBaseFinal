using LoDb.Domain.Builds.Editing;
using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Tests.Builds.Editing;

/// <summary>
/// The steps of the editor, case by case as the legacy <c>stepList</c> spec states them.
/// </summary>
public sealed class StepListTests
{
    [Fact]
    public void StepsAreAddedUpTo10ThenRefused()
    {
        IReadOnlyList<BuildStep> steps = [StepList.CreateStep("Start")];
        while (StepList.CanAddStep(steps))
        {
            steps = StepList.AddStep(steps);
        }

        Assert.Equal(BuildLimits.StepsMax, steps.Count);
        Assert.Same(steps, StepList.AddStep(steps));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 2)]
    [InlineData(-1, 2)]
    public void AStepIsRemovedByIndexOutOfRangeIgnored(int index, int remaining)
    {
        var steps = Steps(["1055"], ["3006"]);

        Assert.Equal(remaining, StepList.RemoveStep(steps, index).Count);
    }

    [Fact]
    public void AStepMovesAndStopsAtTheEdges()
    {
        var steps = Steps(["a"], ["b"], ["c"]);

        Assert.Equal(["b", "a", "c"], Firsts(StepList.MoveStep(steps, 0, 1)));
        Assert.Same(steps, StepList.MoveStep(steps, 0, -1));
        Assert.Same(steps, StepList.MoveStep(steps, 2, 1));
    }

    [Theory]
    [InlineData(0, 3, "b c a")]
    [InlineData(2, 0, "c a b")]
    public void AStepDropsAtAnInsertionPoint(int from, int insert, string expected) =>
        Assert.Equal(
            expected.Split(' '),
            Firsts(StepList.MoveStepToIndex(Steps(["a"], ["b"], ["c"]), from, insert)));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(9, 0)]
    public void ADropNextToItselfChangesNothing(int from, int insert)
    {
        var steps = Steps(["a"], ["b"], ["c"]);

        Assert.Same(steps, StepList.MoveStepToIndex(steps, from, insert));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 0)]
    [InlineData(1, 99)]
    [InlineData(1, -4)]
    [InlineData(0, 0)]
    [InlineData(2, 3)]
    public void TheRestingIndexIsWhereTheEntryLands(int from, int insert)
    {
        var steps = Steps(["a", "b", "c"]);
        var dragged = steps[0].Items[from];

        var moved = StepItems.MoveItemToIndex(steps, new ItemLocation(0, from), insert);

        Assert.Equal(dragged, moved[0].Items[StepList.RestingIndex(from, insert, 3)]);
    }

    internal static IReadOnlyList<BuildStep> Steps(params string[][] items) =>
    [
        .. items.Select(static (ids, i) => new BuildStep
        {
            Label = $"Step {i + 1}",
            Note = null,
            Items = ids,
        }),
    ];

    private static IEnumerable<string> Firsts(IReadOnlyList<BuildStep> steps) =>
        steps.Select(static step => step.Items[0]);
}
