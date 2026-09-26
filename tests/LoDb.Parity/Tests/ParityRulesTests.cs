using LoDb.Parity.Classification;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Tests;

/// <summary>Each rule on the deviation it covers, and on the closest one it must not.</summary>
public sealed class ParityRulesTests
{
    public static TheoryData<string, Deviation> Covered => new()
    {
        {
            "legacy-empty-detail-in-fallback-language",
            Projection("champions", "passive", ("null", "{}")) with
            {
                Tags = Tags(DeviationTags.Detailed, DeviationTags.Fallback),
            }
        },
        {
            "legacy-translated-placeholder-listed",
            Projection("items", "listed", ("true", "false")) with
            {
                Tags = Tags(DeviationTags.Unlisted, DeviationTags.Placeholder),
            }
        },
        { "name-trimmed", Projection("items", "name", ("\"过载 \"", "\"过载\"")) },
        { "legacy-image-not-fetched", Projection("items", "image", ("pending", "present /x.png")) },
        {
            "legacy-ability-icons-on-visit",
            Manifest("champion", DeviationKind.OnlyInNext, Tags(DeviationTags.Ability))
        },
        {
            "legacy-debris-images-not-warmed",
            Manifest("item", DeviationKind.OnlyInNext, Tags(DeviationTags.Unlisted))
        },
    };

    public static TheoryData<Deviation> NotCovered => new()
    {
        // The legacy stack holds the detail in its own language: no excuse.
        Projection("champions", "passive", ("null", "{}")) with
        {
            Tags = Tags(DeviationTags.Detailed),
        },
        // An item the new stack hides though its en_US name declares no placeholder.
        Projection("items", "listed", ("true", "false")) with
        {
            Tags = Tags(DeviationTags.Unlisted),
        },
        // The new stack lists a placeholder the legacy one hides: a new defect.
        Projection("items", "listed", ("false", "true")) with
        {
            Tags = Tags(DeviationTags.Unlisted, DeviationTags.Placeholder),
        },
        Projection("items", "name", ("\"过载\"", "\"超载\"")),
        // The new stack has no verdict where the legacy one has: a new defect.
        Projection("items", "image", ("present /x.png", "pending")),
        Manifest("champion", DeviationKind.OnlyInNext,
            Tags(DeviationTags.Ability, DeviationTags.Detailed)),
        Manifest("champion", DeviationKind.OnlyInNext, Tags(DeviationTags.Portrait)),
        Manifest("champion", DeviationKind.OnlyInLegacy, Tags(DeviationTags.Ability)),
        Manifest("item", DeviationKind.OnlyInNext, Tags()),
        Manifest("item", DeviationKind.Value, Tags(DeviationTags.Unlisted)),
    };

    [Theory]
    [MemberData(nameof(Covered))]
    public void ARuleClassifiesTheDeviationItCovers(string rule, Deviation deviation)
    {
        var classified = Assert.Single(Classifier.Classify([deviation], ParityRules.All));

        Assert.Equal(rule, classified.Rule?.Id);
    }

    [Theory]
    [MemberData(nameof(NotCovered))]
    public void NoRuleClassifiesANearMiss(Deviation deviation)
    {
        var classified = Assert.Single(Classifier.Classify([deviation], ParityRules.All));

        Assert.Null(classified.Rule);
    }

    [Fact]
    public void RuleIdsAreUniqueAndJustified()
    {
        var ids = ParityRules.All.Select(static rule => rule.Id);

        Assert.Equal(ParityRules.All.Count, ids.Distinct().Count());
        Assert.All(ParityRules.All, static rule => Assert.NotEmpty(rule.Justification));
    }

    private static Deviation Projection(string resource, string field, (string, string) values) =>
        new()
        {
            Site = new DeviationSite
            {
                Version = "8.7.1",
                Language = "ar_AE",
                Resource = resource,
                Entry = "1520",
            },
            Kind = DeviationKind.Value,
            Field = field,
            Legacy = values.Item1,
            Next = values.Item2,
        };

    private static Deviation Manifest(string type, DeviationKind kind, IReadOnlySet<string> tags) =>
        new()
        {
            Site = new DeviationSite
            {
                Version = "16.19.1",
                Resource = $"manifest/{type}",
                Entry = "AatroxQ.png",
            },
            Kind = kind,
            Legacy = kind == DeviationKind.OnlyInNext ? null : "present a.png",
            Next = kind == DeviationKind.OnlyInLegacy ? null : "present a.png",
            Tags = tags,
        };

    private static HashSet<string> Tags(params string[] tags) => [.. tags];
}
