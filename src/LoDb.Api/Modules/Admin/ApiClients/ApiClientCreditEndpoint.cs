using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.ApiClients;

/// <summary>
/// <c>POST /api/admin/api-clients/{id}/credit</c>: adds 1 to 1,000,000 prepaid requests to
/// an active key, valid twelve months, spendable at once (<c>admin.api_client_credit</c>).
/// </summary>
internal static class ApiClientCreditEndpoint
{
    public static void Map(IEndpointRouteBuilder clients) =>
        clients.MapPost("/{id:int}/credit", CreditAsync)
            .WithName("creditAdminApiClient")
            .WithSummary("Adds prepaid requests to an active key.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Ok<CreditReceipt>, AccountProblem>> CreditAsync(
        [FromRoute] int id,
        [FromBody] CreditRequest request,
        [FromServices] ApiClientDesk desk,
        CancellationToken cancellationToken)
    {
        var (receipt, problem) = await desk.CreditAsync(id, request?.Requests, cancellationToken);
        return receipt is null ? problem! : TypedResults.Ok(receipt);
    }
}
