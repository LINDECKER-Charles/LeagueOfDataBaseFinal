using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

namespace LoDb.Api.Modules.Admin.Prerender.Options;

/// <summary>
/// What <see cref="PrerenderOptionsValidator"/> refuses of the pace: a floor, a step or a fixed
/// pace above the ceiling (the pass's SSR lane would refuse the renders over its budget), a
/// load URL that is not one, a CPU list that does not parse.
/// </summary>
internal static class PaceOptionsRules
{
    private const string RootPath = "/";

    /// <summary>Each rule <paramref name="options"/> breaks, as a message.</summary>
    public static IEnumerable<string> FailuresOf(PrerenderOptions options) =>
        RateFailuresOf(options).Concat(UrlFailuresOf(options.Pace));

    private static IEnumerable<string> RateFailuresOf(PrerenderOptions options)
    {
        var (pace, most) = (options.Pace, PrerenderOptions.MostRendersPerMinute);
        if (pace.MaxRendersPerMinute < 1 || pace.MaxRendersPerMinute > most)
        {
            yield return $"Pace:MaxRendersPerMinute must be between 1 and {most}.";
        }

        if (pace.MinRendersPerMinute < 1 || pace.MinRendersPerMinute > pace.MaxRendersPerMinute)
        {
            yield return "Pace:MinRendersPerMinute must be between 1 and"
                + " Pace:MaxRendersPerMinute.";
        }

        if (pace.StepRendersPerMinute < 1 || pace.StepRendersPerMinute > pace.MaxRendersPerMinute)
        {
            yield return "Pace:StepRendersPerMinute must be between 1 and"
                + " Pace:MaxRendersPerMinute.";
        }

        if (options.RendersPerMinute > pace.MaxRendersPerMinute)
        {
            yield return "RendersPerMinute, the fixed pace, must not exceed"
                + " Pace:MaxRendersPerMinute: the pass's SSR lane refuses renders over it.";
        }
    }

    private static IEnumerable<string> UrlFailuresOf(PaceOptions pace)
    {
        if (!IsLoadUrl(pace.VisitorsLoadUrl) || !IsLoadUrl(pace.PassLoadUrl))
        {
            yield return "Pace:VisitorsLoadUrl and Pace:PassLoadUrl must be http or https URLs"
                + " with a path, no query nor user info.";
        }

        if (!string.IsNullOrWhiteSpace(pace.HostCpuSet) && CpuSet.Parse(pace.HostCpuSet) is null)
        {
            yield return "Pace:HostCpuSet must be a CPU list: 0-3, or 0,2-3.";
        }
    }

    private static bool IsLoadUrl(string? text) =>
        string.IsNullOrWhiteSpace(text)
        || (Uri.TryCreate(text, UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            && url.AbsolutePath != RootPath
            && url.Query.Length == 0
            && url.Fragment.Length == 0
            && url.UserInfo.Length == 0);
}
