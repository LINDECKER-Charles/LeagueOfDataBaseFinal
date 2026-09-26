using LoDb.Domain.Builds.Rules;
using LoDb.Domain.Tests.Builds.Samples;
using static LoDb.Domain.Builds.Rules.BuildErrors;
using Change = System.Func<
    LoDb.Domain.Builds.Structures.RunePageInput,
    LoDb.Domain.Builds.Structures.RunePageInput>;

namespace LoDb.Domain.Tests.Builds.Rules;

/// <summary>
/// The rune page, case by case as the legacy validator tests state it: one pick per primary
/// slot, keystone first; two secondary picks from distinct minor rows of another tree.
/// </summary>
public sealed class RunePageRulesTests
{
    private static readonly Dictionary<string, (Change Change, string[] Expected)> Cases =
        new(StringComparer.Ordinal)
        {
            ["a complete page passes"] = (static page => page, []),
            ["a non-numeric perk is a slot error"] =
                (static page => page with { PrimarySelections = [8005, null, 9104, 8014] },
                [PrimarySlot]),
            ["an unknown primary tree stops the primary checks"] =
                (static page => page with { PrimaryStyleId = 9999 }, [PrimaryStyle]),
            ["an unreadable primary tree is unknown"] =
                (static page => page with { PrimaryStyleId = null }, [PrimaryStyle]),
            ["three primary picks are too few"] =
                (static page => page with { PrimarySelections = [8005, 9101, 9104] },
                [PrimaryCount]),
            ["five primary picks are too many"] =
                (static page => page with { PrimarySelections = [8005, 9101, 9104, 8014, 8017] },
                [PrimaryCount]),
            ["primary picks that are no list are miscounted"] =
                (static page => page with { PrimarySelections = null }, [PrimaryCount]),
            ["a primary perk of another slot is refused"] =
                (static page => page with { PrimarySelections = [8005, 9104, 9104, 8014] },
                [PrimarySlot]),
            ["a minor cannot sit in the keystone slot"] =
                (static page => page with { PrimarySelections = [9101, 9101, 9104, 8014] },
                [PrimarySlot]),
            ["a primary perk of another tree is refused"] =
                (static page => page with { PrimarySelections = [8005, 8126, 9104, 8014] },
                [PrimarySlot]),
            ["an unknown secondary tree"] =
                (static page => page with { SecondaryStyleId = 4242 }, [SecondaryStyle]),
            ["the secondary tree must differ from the primary"] =
                (static page => page with
                {
                    SecondaryStyleId = BuildSamples.Precision,
                    SecondarySelections = [9101, 9104],
                },
                [SecondarySameStyle]),
            ["one secondary pick is too few"] =
                (static page => page with { SecondarySelections = [8126] },
                [SecondaryCount]),
            ["three secondary picks are too many"] =
                (static page => page with { SecondarySelections = [8126, 8138, 8106] },
                [SecondaryCount]),
            ["a keystone is forbidden in the secondary tree"] =
                (static page => page with { SecondarySelections = [8112, 8138] },
                [SecondarySlot]),
            ["a secondary perk must belong to the secondary tree"] =
                (static page => page with { SecondarySelections = [9101, 8138] },
                [SecondarySlot]),
            ["two secondary picks of one row are refused"] =
                (static page => page with { SecondarySelections = [8126, 8139] },
                [SecondarySameSlot]),
            ["two secondary picks of distinct rows pass"] =
                (static page => page with { SecondarySelections = [8139, 8135] }, []),
            ["both sides are reported together"] =
                (static page => page with { PrimaryStyleId = 1, SecondarySelections = [] },
                [PrimaryStyle, SecondaryCount]),
        };

    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void ThePageIsCheckedAsTheLegacyValidatorDid(string name)
    {
        var (change, expected) = Cases[name];

        var errors = BuildStructureValidator.Validate(
            BuildSamples.WithRunes(change),
            BuildSamples.Catalog);

        Assert.Equal(expected, errors);
    }
}
