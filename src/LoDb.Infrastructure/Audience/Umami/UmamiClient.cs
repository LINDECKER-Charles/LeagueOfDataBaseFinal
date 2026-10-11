using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Audience.Umami;

/// <summary>
/// The read-only side of Umami's API that the pull needs (Umami 3.4.0): sign in, the page
/// views and visitors per path of a span, the page views of the whole site over a span.
/// </summary>
/// <remarks>
/// Through the <c>umami</c> client only, whose allow-list is the configured host. The token
/// lives for one pull and is never logged; an answer other than a success is an
/// <see cref="UmamiException"/> that names the status, never the body.
/// </remarks>
internal sealed class UmamiClient(IHttpClientFactory clients, IOptions<AudienceOptions> options)
{
    /// <summary>Name of the client in <see cref="IHttpClientFactory"/>.</summary>
    public const string ClientName = "umami";

    private const string SignInPath = "/api/auth/login";
    private const string WebsitesPath = "/api/websites/";
    private const string PathsMetric = "/metrics/expanded?type=path";
    private const string StatsPath = "/stats";
    private const string BearerScheme = "Bearer";

    // Umami writes a path's page views as a string and its visitors as a number.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>A token of the read-only account, valid for the pull that asks it.</summary>
    public async Task<string> SignInAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var response = await Client().PostAsJsonAsync(
            Url(SignInPath),
            new SignIn(settings.Username ?? string.Empty, settings.Password ?? string.Empty),
            Json,
            cancellationToken);
        var reply = await ReadAsync<SignInReply>(response, SignInPath, cancellationToken);
        return string.IsNullOrEmpty(reply.Token)
            ? throw new UmamiException("Umami signed the account in without a token.")
            : reply.Token;
    }

    /// <summary>
    /// Every path counted during <paramref name="window"/>, the most viewed first, a page of
    /// <see cref="AudienceOptions.PageSize"/> at a time and
    /// <see cref="AudienceOptions.MaxPages"/> pages at most, each path once.
    /// </summary>
    /// <remarks>
    /// Umami orders the paths by their counts alone: rows that tie may come back in another
    /// order for another offset, so a path can show up on two pages. It is counted once.
    /// </remarks>
    public async Task<IReadOnlyList<UmamiPathViews>> ReadPathsAsync(
        string token,
        UmamiWindow window,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var paths = new List<UmamiPathViews>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; page < settings.MaxPages; page++)
        {
            var offset = page * settings.PageSize;
            var rows = await GetAsync<PathRow[]>(
                token,
                PathsQuery(window, offset),
                cancellationToken);
            paths.AddRange(rows
                .Select(static row => new UmamiPathViews(
                    row.Name ?? string.Empty,
                    row.Pageviews,
                    row.Visitors))
                .Where(path => seen.Add(path.Path)));
            if (rows.Length < settings.PageSize)
            {
                break;
            }
        }

        return paths;
    }

    /// <summary>The page views of the whole site during <paramref name="window"/>.</summary>
    public async Task<long> ReadPageViewsAsync(
        string token,
        UmamiWindow window,
        CancellationToken cancellationToken)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"{WebsiteRoot()}{StatsPath}?startAt={window.StartAt}&endAt={window.EndAt}");
        return (await GetAsync<StatsReply>(token, query, cancellationToken)).Pageviews;
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new UmamiException(
                $"Umami answered {(int)response.StatusCode} to {path.Split('?')[0]}.");
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new UmamiException($"Umami answered an empty body to {path.Split('?')[0]}.");
    }

    private async Task<T> GetAsync<T>(
        string token,
        string pathAndQuery,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(pathAndQuery));
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token);
        using var response = await Client().SendAsync(request, cancellationToken);
        return await ReadAsync<T>(response, pathAndQuery, cancellationToken);
    }

    private string PathsQuery(UmamiWindow window, int offset) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{WebsiteRoot()}{PathsMetric}&startAt={window.StartAt}&endAt={window.EndAt}"
            + $"&limit={options.Value.PageSize}&offset={offset}");

    private string WebsiteRoot() =>
        WebsitesPath + options.Value.WebsiteId?.ToString("D", CultureInfo.InvariantCulture);

    private Uri Url(string pathAndQuery) => new($"https://{options.Value.Host}{pathAndQuery}");

    private HttpClient Client() => clients.CreateClient(ClientName);

    private sealed record SignIn(string Username, string Password);

    private sealed record SignInReply(string? Token);

    private sealed record PathRow(string? Name, long Pageviews, long Visitors);

    private sealed record StatsReply(long Pageviews);
}
