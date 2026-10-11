using System.Text;
using LoDb.Api.Hosting.SecurityReports;

namespace LoDb.Api.Tests.Hosting.SecurityReports;

/// <summary>
/// The violations of a report body, kept to a bounded vocabulary whatever the report says.
/// </summary>
public sealed class CspReportFormatsTests
{
    [Theory]
    [InlineData("script-src-elem", "script-src-elem")]
    [InlineData("SCRIPT-SRC 'self' 'sha256-x'", "script-src")]
    [InlineData("<script>alert(1)</script>", "other")]
    [InlineData("", "other")]
    public void KeepsADirectiveOfTheVocabularyOnly(string written, string counted)
    {
        var body = $$$"""{"csp-report": {"effective-directive": "{{{written}}}"}}""";

        var violation = Assert.Single(CspReportFormats.Parse(Bytes(body)) ?? []);

        Assert.Equal(counted, violation.Directive);
    }

    [Theory]
    [InlineData("enforce", "enforce")]
    [InlineData("report", "report")]
    [InlineData("anything", "unknown")]
    public void KeepsADispositionOfTheVocabularyOnly(string written, string counted)
    {
        var body = $$$"""
            [{"type": "csp-violation",
              "body": {"effectiveDirective": "img-src", "disposition": "{{{written}}}"}}]
            """;

        var violation = Assert.Single(CspReportFormats.Parse(Bytes(body)) ?? []);

        Assert.Equal(counted, violation.Disposition);
    }

    [Fact]
    public void ReadsNoMoreReportsThanABrowserBatches()
    {
        var report = """{"type": "csp-violation", "body": {"effectiveDirective": "img-src"}}""";
        var body = $"[{string.Join(',', Enumerable.Repeat(report, 150))}]";

        var violations = CspReportFormats.Parse(Bytes(body));

        Assert.Equal(CspReportFormats.MaxReportsPerBody, violations?.Count);
    }

    [Theory]
    [InlineData("\"text\"")]
    [InlineData("{\"other\": {}}")]
    [InlineData("{\"csp-report\": \"text\"}")]
    [InlineData("[{\"type\": ")]
    public void FindsNothingInWhatIsNotAReport(string body) =>
        Assert.Null(CspReportFormats.Parse(Bytes(body)));

    private static ReadOnlyMemory<byte> Bytes(string text) => Encoding.UTF8.GetBytes(text);
}
