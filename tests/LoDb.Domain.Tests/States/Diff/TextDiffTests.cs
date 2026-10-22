using LoDb.Domain.Languages;
using LoDb.Domain.States.Diff;

namespace LoDb.Domain.Tests.States.Diff;

/// <summary>
/// A text diff in segments of plain text: by words where spaces separate them, by text
/// element in Japanese, Chinese and Thai.
/// </summary>
public sealed class TextDiffTests
{
    private const int RewrittenWords = 700;

    [Fact]
    public void WordsAddedShowAsOneSegment()
    {
        var segments = TextDiff.Of(
            "Limited to 1.",
            "Limited to 1 pair of boots.",
            DdragonLanguage.EnUs);

        Assert.Equal(
            [Same("Limited to 1"), Added(" pair of boots"), Same(".")],
            segments);
    }

    [Fact]
    public void AWordReplacedIsRemovedThenAdded()
    {
        var segments = TextDiff.Of(
            "Grants 30 armor.",
            "Grants 35 armor.",
            DdragonLanguage.EnUs);

        Assert.Equal(
            [Same("Grants "), Removed("30"), Added("35"), Same(" armor.")],
            segments);
    }

    [Theory]
    [InlineData("zh_CN", "获得30护甲", "获得35护甲")]
    [InlineData("ja_JP", "物理防御30を得る", "物理防御35を得る")]
    [InlineData("th_TH", "ได้รับเกราะ30หน่วย", "ได้รับเกราะ35หน่วย")]
    public void UnspacedLanguagesSplitByTextElement(string code, string before, string after)
    {
        var segments = TextDiff.Of(before, after, DdragonLanguage.Parse(code));

        Assert.Contains(Removed("0"), segments);
        Assert.Contains(Added("5"), segments);
        Assert.Equal(before, string.Concat(Kept(segments, SegmentKind.Added)));
        Assert.Equal(after, string.Concat(Kept(segments, SegmentKind.Removed)));
    }

    [Fact]
    public void ASpacedLanguageKeepsAnUnspacedSentenceWhole()
    {
        var segments = TextDiff.Of("获得30护甲", "获得35护甲", DdragonLanguage.EnUs);

        Assert.Equal([Removed("获得30护甲"), Added("获得35护甲")], segments);
    }

    [Fact]
    public void AMiddleRewrittenBeyondTheTableIsRemovedThenAdded()
    {
        // 700 words a side: some 1,400 tokens each, past the million cells of the table.
        var before = Rewritten("old");
        var after = Rewritten("new");

        var segments = TextDiff.Of(before, after, DdragonLanguage.EnUs);

        Assert.Equal(
            [
                Same("Opening "),
                Removed(Words("old")),
                Added(Words("new")),
                Same(" closing."),
            ],
            segments);
    }

    [Fact]
    public void EqualTextsAreOneSameSegment()
    {
        Assert.Equal(
            [Same("Unchanged.")],
            TextDiff.Of("Unchanged.", "Unchanged.", DdragonLanguage.EnUs));
    }

    [Fact]
    public void ATextFromNothingIsAllAdded()
    {
        Assert.Equal([Added("New.")], TextDiff.Of(string.Empty, "New.", DdragonLanguage.EnUs));
    }

    private static string Rewritten(string prefix) => $"Opening {Words(prefix)} closing.";

    private static string Words(string prefix) => string.Join(
        ' ',
        Enumerable.Range(0, RewrittenWords).Select(index => prefix + index));

    // The text one side reads: every segment but those of the other side.
    private static IEnumerable<string> Kept(
        IReadOnlyList<TextSegment> segments,
        SegmentKind otherSide) =>
        segments
            .Where(segment => segment.Kind != otherSide)
            .Select(static segment => segment.Text);

    private static TextSegment Same(string text) => new() { Kind = SegmentKind.Same, Text = text };

    private static TextSegment Removed(string text) =>
        new() { Kind = SegmentKind.Removed, Text = text };

    private static TextSegment Added(string text) =>
        new() { Kind = SegmentKind.Added, Text = text };
}
