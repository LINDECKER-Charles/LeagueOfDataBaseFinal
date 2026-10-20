using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace LoDb.Api.Tests.Admin.Scaleway.Support;

/// <summary>
/// Answers of the billing API, built from a real answer of October 2026 whose ids and names
/// were replaced by fakes: the backups' project with its free-trial deductions, the pages'
/// project with a deduction of the free egress, and a third project of the organization.
/// </summary>
internal static class ScalewayAnswers
{
    public const string OrganizationId = "99999999-9999-4999-8999-999999999999";
    public const string BackupsProjectId = "11111111-1111-4111-8111-111111111111";
    public const string PagesProjectId = "22222222-2222-4222-8222-222222222222";

    /// <summary>The month of the fixture, the current one of the tests' clock.</summary>
    public const string CurrentPeriod = "2026-10";

    /// <summary>
    /// Scaleway's <c>updated_at</c> in the fixture: the time of the query, which the bill
    /// leaves out.
    /// </summary>
    public static readonly DateTimeOffset UpdatedAt = DateTimeOffset.Parse(
        "2026-10-20T17:43:44.738445Z",
        CultureInfo.InvariantCulture);

    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "fixtures", "scaleway", "consumptions-object-storage.json");

    /// <summary>The fixture's lines.</summary>
    public static JsonArray Lines() =>
        JsonNode.Parse(File.ReadAllText(FixturePath))!["consumptions"]!.AsArray();

    /// <summary>The fixture for its month, nothing for the others.</summary>
    public static HttpResponseMessage Default(ScalewayCall call) =>
        call.Period == CurrentPeriod ? Page([.. Lines()], totalCount: null) : Empty();

    /// <summary>
    /// A page holding <paramref name="lines"/>, of a list of <paramref name="totalCount"/>
    /// lines, as many as it holds when null.
    /// </summary>
    public static HttpResponseMessage Page(IReadOnlyList<JsonNode?> lines, int? totalCount)
    {
        var page = new JsonObject
        {
            ["consumptions"] =
                new JsonArray([.. lines.Select(static line => line?.DeepClone())]),
            ["total_count"] = totalCount ?? lines.Count,
            ["total_discount_untaxed_value"] = 0,
            ["updated_at"] =
                UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        };
        return Json(page.ToJsonString());
    }

    /// <summary>A project without consumption, as Scaleway answers it.</summary>
    public static HttpResponseMessage Empty() =>
        Json(
            """
            {"consumptions":[],"total_count":0,"total_discount_untaxed_value":0,"updated_at":null}
            """);

    public static HttpResponseMessage Json(
        string body,
        HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
