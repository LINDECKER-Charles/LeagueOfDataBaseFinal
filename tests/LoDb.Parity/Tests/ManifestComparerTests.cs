using LoDb.Parity.Deviations;
using LoDb.Parity.Manifests;

namespace LoDb.Parity.Tests;

public sealed class ManifestComparerTests
{
    private const string Sha = "852d739fb696684549064266851df91ecd03729e06b1a192dc14af01549b7f3f";

    [Fact]
    public void TheSameBlobAndTheSameAbsenceMatch()
    {
        var found = Compare(
            new() { ["Aatrox.png"] = $"cdn/blobs/{Sha}.png", ["Old.png"] = null },
            [Present("Aatrox.png", Sha), Absent("Old.png")]);

        Assert.Empty(found);
    }

    [Fact]
    public void AnotherContentIsAValueDeviation()
    {
        var deviation = Assert.Single(Compare(
            new() { ["Aatrox.png"] = $"cdn/blobs/{Sha}.png" },
            [Present("Aatrox.png", Sha.Replace('8', '9'))]));

        Assert.Equal(DeviationKind.Value, deviation.Kind);
        Assert.Equal(
            ("manifest/champion", "Aatrox.png"),
            (deviation.Site.Resource, deviation.Site.Entry));
        Assert.Null(deviation.Site.Language);
    }

    [Fact]
    public void AbsentOnOneSideOnlyIsAValueDeviation()
    {
        var deviation = Assert.Single(Compare(
            new() { ["Aatrox.png"] = null },
            [Present("Aatrox.png", Sha)]));

        Assert.Equal(("absent", $"present {Sha}.png"), (deviation.Legacy, deviation.Next));
    }

    [Fact]
    public void KeysOnOneSideAreReportedWithTheirTags()
    {
        var found = Compare(
            new() { ["AatroxQ.png"] = $"cdn/blobs/{Sha}.png" },
            [Present("AatroxW.png", Sha)],
            new Dictionary<string, IReadOnlySet<string>>
            {
                ["AatroxW.png"] = new HashSet<string> { DeviationTags.Ability },
            });

        Assert.Equal(
            [
                (DeviationKind.OnlyInLegacy, "AatroxQ.png"),
                (DeviationKind.OnlyInNext, "AatroxW.png"),
            ],
            found.Select(static d => (d.Kind, d.Site.Entry)));
        Assert.Contains(DeviationTags.Ability, found[1].Tags);
    }

    [Theory]
    [InlineData("cdn/blobs/ab.png", "present ab.png")]
    [InlineData(null, "absent")]
    public void LegacyValuesReadAsVerdicts(string? path, string verdict) =>
        Assert.Equal(verdict, ManifestComparer.LegacyVerdict(path));

    private static IReadOnlyList<Deviation> Compare(
        Dictionary<string, string?> legacy,
        ManifestRow[] next,
        Dictionary<string, IReadOnlySet<string>>? tags = null) =>
        ManifestComparer.Compare(new ManifestPair
        {
            Version = "16.19.1",
            Type = "champion",
            Legacy = legacy,
            Next = next.ToDictionary(static row => row.Key),
            Tags = tags ?? [],
        });

    private static ManifestRow Present(string key, string sha) => new()
    {
        Version = "16.19.1",
        Type = "champion",
        Key = key,
        Status = "present",
        Sha256 = sha,
        Extension = "png",
    };

    private static ManifestRow Absent(string key) => new()
    {
        Version = "16.19.1",
        Type = "champion",
        Key = key,
        Status = "absent",
    };
}
