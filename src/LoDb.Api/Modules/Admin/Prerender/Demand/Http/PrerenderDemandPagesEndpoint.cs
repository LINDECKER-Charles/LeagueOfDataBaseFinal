using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>
/// <c>GET /api/admin/prerender/demand/pages?state={state}&amp;page={n}</c>: the pages of the
/// automatic queue in one state, 50 a page; <c>400</c> for a state the queue has not, or a
/// page below 1.
/// </summary>
internal static class PrerenderDemandPagesEndpoint
{
    /// <summary>The field a missing or unknown state is refused under.</summary>
    public const string StateField = "state";

    /// <summary>The field a page out of range is refused under.</summary>
    public const string PageField = "page";

    private const int FirstPage = 1;

    public static void Map(IEndpointRouteBuilder prerender) =>
        prerender.MapGet("/demand/pages", ReadAsync)
            .WithName("readAdminPrerenderDemandPages")
            .WithSummary("The pages of the automatic queue in one state, a page of 50.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<PrerenderDemandPages>, AccountProblem>> ReadAsync(
        [FromQuery] string? state,
        [FromQuery] int? page,
        [FromServices] DemandPagesReader reader,
        CancellationToken cancellationToken)
    {
        var errors = new FieldErrors();
        errors.RequireShape(StateField, state, static text => StateOf(text) is not null);
        var number = page ?? FirstPage;
        if (number is < FirstPage or > DemandPagesReader.MaxPage)
        {
            errors.Add(PageField, FieldErrors.Malformed);
        }

        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        var listed = await reader.ReadAsync(StateOf(state!)!.Value, number, cancellationToken);
        return TypedResults.Ok(listed);
    }

    // The states as the table spells them; any other text is none.
    private static PrerenderDemandState? StateOf(string text)
    {
        foreach (var known in Enum.GetValues<PrerenderDemandState>())
        {
            if (string.Equals(PrerenderDemandColumns.ToText(known), text, StringComparison.Ordinal))
            {
                return known;
            }
        }

        return null;
    }
}
