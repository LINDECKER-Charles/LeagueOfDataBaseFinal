using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace LoDb.Infrastructure.Tests.Audience.Support;

/// <summary>
/// Umami 3.4.0 as the pull sees it, answering in the shapes measured on the real server: the
/// sign-in, the page views and visitors per path of a span (page views as a string), the
/// page views of the site over a span. Every request is kept, its credentials included.
/// </summary>
internal sealed class FakeUmami : HttpMessageHandler
{
    public const string Token = "umami-session-token";

    private const string SignInPath = "/api/auth/login";
    private const string PathsSuffix = "/metrics/expanded";
    private const string StatsSuffix = "/stats";
    private const string Bearer = "Bearer " + Token;
    private const string JsonType = "application/json";

    private readonly List<FakeUmamiRequest> _requests = [];

    /// <summary>The paths counted on each UTC day: path, page views, visitors.</summary>
    public Dictionary<DateOnly, (string Path, long Views, long Visitors)[]> Days { get; } = [];

    /// <summary>The page views of the site over any span the stats are asked for.</summary>
    public long SiteViews { get; set; }

    /// <summary>What the sign-in answers.</summary>
    public HttpStatusCode SignInStatus { get; set; } = HttpStatusCode.OK;

    public IReadOnlyList<FakeUmamiRequest> Requests => _requests;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var authorization = request.Headers.Authorization?.ToString();
        _requests.Add(new(request.Method, request.RequestUri!, authorization, body));
        var path = request.RequestUri!.AbsolutePath;
        if (path == SignInPath)
        {
            return SignInStatus == HttpStatusCode.OK
                ? Json(new { token = Token, user = new { id = "reader" } })
                : new HttpResponseMessage(SignInStatus);
        }

        if (authorization != Bearer)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return path.EndsWith(PathsSuffix, StringComparison.Ordinal)
            ? Paths(request.RequestUri)
            : path.EndsWith(StatsSuffix, StringComparison.Ordinal)
                ? Json(new { pageviews = SiteViews, visitors = 1, visits = 1, bounces = 0 })
                : new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, JsonType),
        };

    private static long Number(Dictionary<string, StringValues> query, string key) =>
        long.Parse(query[key].ToString(), CultureInfo.InvariantCulture);

    private HttpResponseMessage Paths(Uri url)
    {
        var query = QueryHelpers.ParseQuery(url.Query);
        var start = DateTimeOffset.FromUnixTimeMilliseconds(Number(query, "startAt"));
        var day = DateOnly.FromDateTime(start.UtcDateTime);
        var rows = Days.GetValueOrDefault(day, [])
            .Skip((int)Number(query, "offset"))
            .Take((int)Number(query, "limit"))
            .Select(static row => new
            {
                name = row.Path,
                pageviews = row.Views.ToString(CultureInfo.InvariantCulture),
                visitors = row.Visitors,
                visits = row.Visitors,
                bounces = 0,
                totaltime = 0,
            });
        return Json(rows);
    }
}
