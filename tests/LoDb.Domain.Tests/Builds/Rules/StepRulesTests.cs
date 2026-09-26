using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Tests.Builds.Samples;
using static LoDb.Domain.Builds.Rules.BuildErrors;
using Change = System.Func<
    LoDb.Domain.Builds.Structures.BuildStructureInput,
    LoDb.Domain.Builds.Structures.BuildStructureInput>;
using StepChange = System.Func<
    LoDb.Domain.Builds.Structures.StepInput,
    LoDb.Domain.Builds.Structures.StepInput>;

namespace LoDb.Domain.Tests.Builds.Rules;

/// <summary>
/// The purchase order, case by case as the legacy validator tests state it: 1 to 10 steps,
/// a label of 40 characters, a note of 300, 1 to 8 known items per step and 40 in all.
/// </summary>
public sealed class StepRulesTests
{
    // One character beyond the Basic Multilingual Plane: two UTF-16 code units.
    private const string Shield = "\U0001F6E1";

    private static readonly StepInput Plain = BuildSamples.Step("S", null, "1055");

    private static readonly Dictionary<string, (Change Change, string[] Expected)> Cases =
        new(StringComparer.Ordinal)
        {
            ["no step at all"] = (Steps(), [StepsCount]),
            ["eleven steps"] = (Steps([.. Enumerable.Repeat(Plain, 11)]), [StepsCount]),
            ["steps that are no list"] =
                (static s => s with { Steps = null }, [StepsCount]),
            ["ten steps pass"] = (Steps([.. Enumerable.Repeat(Plain, 10)]), []),
            ["a step that is no object"] =
                (static s => s with { Steps = [null, .. s.Steps!.Skip(1)] },
                [StructureInvalid]),
            ["an empty label"] = (First(static t => t with { Label = "" }), [StepLabel]),
            ["a blank label"] = (First(static t => t with { Label = "  " }), [StepLabel]),
            ["a missing label"] = (First(static t => t with { Label = null }), [StepLabel]),
            ["a label of 41 characters"] = (Label(new string('x', 41)), [StepLabel]),
            ["a label of 40 characters padded"] = (Label($"  {new string('x', 40)}  "), []),
            ["a label of 40 emoji"] = (Label(string.Concat(Enumerable.Repeat(Shield, 40))), []),
            ["a note of 301 characters"] = (Note(new string('n', 301)), [StepNote]),
            ["a note of 300 characters"] = (Note(new string('n', 300)), []),
            ["a note that is no string"] =
                (First(static t => t with { IsNoteMalformed = true }), [StepNote]),
            ["a step without items"] = (Items(), [StepItemsCount]),
            ["a step of nine items"] =
                (Items(BuildSamples.Repeat("1055", 9)), [StepItemsCount]),
            ["items that are no list"] =
                (First(static t => t with { Items = null }), [StepItemsCount]),
            ["an unknown item"] = (Items("1055", "9999"), [StepItemUnknown]),
            ["an item that is no scalar"] = (Items("1055", null), [StepItemUnknown]),
            ["duplicate items are legitimate"] = (Items("2003", "2003", "2003"), []),
            ["forty items in all pass"] = (Steps([.. BuildSamples.CappedSteps()]), []),
            ["forty-one items in all"] =
                (Steps([.. BuildSamples.CappedSteps(), BuildSamples.Step("S6", null, "3006")]),
                [StepsTotalItems]),
            ["an overfilled step does not also inflate the total"] =
                (Steps([.. BuildSamples.CappedSteps(), BuildSamples.Step("S6", null, Nine())]),
                [StepItemsCount]),
        };

    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void TheStepsAreCheckedAsTheLegacyValidatorDid(string name)
    {
        var (change, expected) = Cases[name];

        var errors = BuildStructureValidator.Validate(
            change(BuildSamples.Valid),
            BuildSamples.Catalog);

        Assert.Equal(expected, errors);
    }

    private static Change Steps(params StepInput?[] steps) =>
        structure => structure with { Steps = steps };

    private static Change First(StepChange change) =>
        structure => structure with
        {
            Steps = [change(structure.Steps![0]!), .. structure.Steps.Skip(1)],
        };

    private static Change Label(string label) => First(step => step with { Label = label });

    private static Change Note(string note) => First(step => step with { Note = note });

    private static Change Items(params string?[] items) =>
        First(step => step with { Items = items });

    private static string[] Nine() => BuildSamples.Repeat("1055", 9);
}
