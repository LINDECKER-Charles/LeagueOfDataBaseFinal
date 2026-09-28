using System.Text.Json.Nodes;

namespace LoDb.Api.Tests.PublicApi.Contract;

/// <summary>
/// Where the new <c>/v1</c> answers otherwise than go-api on purpose, request by request:
/// each departure is listed in <c>docs/reecriture/rapports/contrat-v1.md</c>.
/// </summary>
/// <remarks>
/// The pages of builds, which the departure on banned authors reshapes, are worked out by
/// <see cref="BuildsOracle"/> instead.
/// </remarks>
internal static class ContractDeviations
{
    private const string TextContentType = "text/plain; charset=utf-8";

    /// <summary>
    /// The answer expected instead of the recorded one, or null when the recorded one holds.
    /// </summary>
    public static RecordedResponse? Replace(
        string group,
        RecordedExchange exchange,
        IReadOnlyList<RecordedExchange> exchanges)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        var recorded = exchange.Response!;
        return (group, exchange.Id) switch
        {
            // A banned account keeps no public profile: go-api still served it.
            ("03-profiles", "banned") => recorded with
            {
                Status = 404,
                Body = Envelope("not_found", "no public profile for this username"),
            },

            // An empty segment is a path no route spells: go-api's router redirected it.
            ("04-builds", "missing-champion-segment") => GoNotFoundUnderV1(),

            // A key changed through IApiKeyCache is read again at once, not a minute later:
            // each answer is the one go-api gave once its cache had expired.
            ("09-key-cache", "revoked-still-served") => AnswerTo(exchanges, "revoked-refused"),
            ("09-key-cache", "created-still-unknown") => AnswerTo(exchanges, "created-served"),
            ("09-key-cache", "topped-up-still-refused") =>
                AnswerTo(exchanges, "topped-up-served"),
            ("09-key-cache", "upgraded-old-limit") => AnswerTo(exchanges, "upgraded-new-limit"),

            // The trends read analytics_daily: with the database down they fail as the other
            // routes do, where go-api read files, then served an empty ranking without them.
            ("10-outage", "trends-database-down" or "trends-storage-down") => recorded with
            {
                Status = 503,
                Body = Envelope("internal", "service temporarily unavailable"),
            },
            _ => null,
        };
    }

    /// <summary>
    /// Whether the request is outside the <c>/v1</c> contract (<c>/healthz</c>, <c>/</c>):
    /// only the absence of the CORS headers of <c>/v1</c> is checked there.
    /// </summary>
    public static bool IsOutsideContract(RecordedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !request.Path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase);
    }

    private static RecordedResponse AnswerTo(
        IReadOnlyList<RecordedExchange> exchanges,
        string id) => exchanges.Single(exchange => exchange.Id == id).Response!;

    private static RecordedResponse GoNotFoundUnderV1() => new(
        404,
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["access-control-allow-headers"] = "Authorization, X-Api-Key",
            ["access-control-allow-methods"] = "GET, OPTIONS",
            ["access-control-allow-origin"] = "*",
            ["content-type"] = TextContentType,
            ["x-content-type-options"] = "nosniff",
        },
        null,
        "404 page not found\n");

    private static JsonObject Envelope(string code, string message) => new()
    {
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };
}
