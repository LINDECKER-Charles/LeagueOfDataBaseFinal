using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Tests.Prerender.Support;

namespace LoDb.Api.Tests.Prerender;

/// <summary>
/// What the pass makes of each answer of the SSR: only a 200 marked <c>archived</c>, gzip and
/// named after its build is stored; one answer per class of status and mark. The verdicts are
/// named, the enumeration being the API's own.
/// </summary>
public sealed class RenderVerdictsTests
{
    // The marks of an answer, by word: its class mark, its gzip encoding, its build.
    private const string Archived = "archived";
    private const string Gzip = "gzip";
    private const string Build = "build";

    public static TheoryData<HttpStatusCode, string, string> Answers => new()
    {
        { HttpStatusCode.OK, "archived gzip build", nameof(RenderVerdict.Storable) },
        { HttpStatusCode.OK, "archived build", nameof(RenderVerdict.Transient) },
        { HttpStatusCode.OK, "archived gzip", nameof(RenderVerdict.Transient) },
        { HttpStatusCode.OK, "gzip build", nameof(RenderVerdict.Skipped) },
        { HttpStatusCode.MovedPermanently, string.Empty, nameof(RenderVerdict.Skipped) },
        { HttpStatusCode.NotFound, string.Empty, nameof(RenderVerdict.Skipped) },
        { HttpStatusCode.ServiceUnavailable, string.Empty, nameof(RenderVerdict.Refused) },
        { HttpStatusCode.TooManyRequests, string.Empty, nameof(RenderVerdict.Refused) },
        { HttpStatusCode.BadGateway, string.Empty, nameof(RenderVerdict.Transient) },
        { HttpStatusCode.BadRequest, string.Empty, nameof(RenderVerdict.Misconfigured) },
    };

    [Theory]
    [MemberData(nameof(Answers))]
    public void AnswerIsReadAsTheContractSays(
        HttpStatusCode status,
        string marks,
        string expected)
    {
        var words = marks.Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = FakeSsr.ArchivedCaching,
        };
        Add(headers, "X-LoDb-Cache-Class", words.Contains(Archived) ? Archived : null);
        Add(headers, "Content-Encoding", words.Contains(Gzip) ? Gzip : null);
        Add(headers, "X-LoDb-Build", words.Contains(Build) ? FakeSsr.BuildA : null);

        var verdict = RenderVerdicts.Of(new RenderedPage { Status = status, Headers = headers });

        Assert.Equal(Enum.Parse<RenderVerdict>(expected), verdict);
    }

    [Theory]
    [InlineData("private, no-store", nameof(RenderVerdict.Transient))]
    [InlineData("public, max-age=0, s-maxage=1800", nameof(RenderVerdict.Skipped))]
    public void UnmarkedPageKeptOutOfEveryCacheIsTriedAgain(
        string cacheControl,
        string expected)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = cacheControl,
            ["Content-Encoding"] = Gzip,
        };

        var verdict = RenderVerdicts.Of(
            new RenderedPage { Status = HttpStatusCode.OK, Headers = headers });

        Assert.Equal(Enum.Parse<RenderVerdict>(expected), verdict);
    }

    private static void Add(Dictionary<string, string> headers, string name, string? value)
    {
        if (value is not null)
        {
            headers[name] = value;
        }
    }
}
