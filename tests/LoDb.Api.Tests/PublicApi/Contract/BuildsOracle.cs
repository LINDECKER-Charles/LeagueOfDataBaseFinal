using System.Globalization;
using System.Text.Json.Nodes;
using LoDb.Api.Tests.PublicApi.Support;
using Npgsql;

namespace LoDb.Api.Tests.PublicApi.Contract;

/// <summary>
/// A page of <c>/v1/champions/{id}/builds</c> worked out from the database, apart from the
/// API: go-api's page when the builds of banned authors are listed, the new one otherwise.
/// </summary>
/// <remarks>
/// The references cannot give the new pages: dropping the two builds of the banned author
/// shifts every page onto builds the capture never showed. So the contract test checks this
/// oracle against every recorded page, banned authors listed, then the API against it.
/// </remarks>
internal static class BuildsOracle
{
    private const string ShareUrlPrefix = "https://league-of-data-base.com/b/";
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static async Task<JsonObject> PageAsync(
        V1Database database,
        string championId,
        long page,
        int perPage,
        bool bannedAuthorsListed)
    {
        ArgumentNullException.ThrowIfNull(database);
        var builds = await PublicBuildsAsync(database, championId);
        var listed = builds.Where(build => bannedAuthorsListed || !build.BannedAuthor).ToList();
        var totalPages = listed.Count == 0 ? 1 : ((listed.Count - 1) / perPage) + 1;
        var data = page > totalPages
            ? []
            : listed.Skip((int)((page - 1) * perPage)).Take(perPage).Select(Item);
        return new JsonObject
        {
            ["champion_id"] = championId,
            ["data"] = new JsonArray([.. data]),
            ["pagination"] = new JsonObject
            {
                ["page"] = page,
                ["per_page"] = perPage,
                ["total"] = listed.Count,
                ["total_pages"] = totalPages,
            },
        };
    }

    private static async Task<List<PublicBuild>> PublicBuildsAsync(
        V1Database database,
        string championId)
    {
        await using var command = database.DataSource.CreateCommand(
            """
            SELECT b.name, b.description, b.game_version, b.runes::text, b.steps::text,
                   b.share_token, b.created_at, u.is_banned
              FROM builds b JOIN users u ON u.id = b.owner_id
             WHERE b.champion_id = $1 AND b.is_public
             ORDER BY b.created_at DESC, b.id DESC
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = championId });
        await using var reader = await command.ExecuteReaderAsync(V1Server.Token);
        var builds = new List<PublicBuild>();
        while (await reader.ReadAsync(V1Server.Token))
        {
            builds.Add(new PublicBuild(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetFieldValue<DateTime>(6),
                reader.GetBoolean(7)));
        }

        return builds;
    }

    private static JsonNode Item(PublicBuild build) => new JsonObject
    {
        ["name"] = build.Name,
        ["description"] = build.Description,
        ["game_version"] = build.GameVersion,
        ["runes"] = JsonNode.Parse(build.Runes),
        ["steps"] = JsonNode.Parse(build.Steps),
        ["share_url"] = ShareUrlPrefix + build.ShareToken,
        ["created_at"] = build.CreatedAt.ToString(DateFormat, CultureInfo.InvariantCulture),
    };

    private sealed record PublicBuild(
        string Name,
        string? Description,
        string GameVersion,
        string Runes,
        string Steps,
        string ShareToken,
        DateTime CreatedAt,
        bool BannedAuthor);
}
