using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Api.Modules.Admin.Scaleway.Wire;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// Reads the Object Storage lines of the organization from Scaleway's billing API, through
/// the <c>scaleway-billing</c> client, every page of each period.
/// </summary>
/// <remarks>
/// The key goes in the header of each request, never in the client's defaults nor in a URL,
/// and the client logs no request: no line ever holds it. One query per period for the
/// whole organization; the lines carry their project.
/// </remarks>
internal sealed class ScalewayBillingClient(
    IHttpClientFactory clients,
    IOptions<ScalewayBillingOptions> options)
{
    /// <summary>Name of the client in <see cref="IHttpClientFactory"/>.</summary>
    public const string ClientName = "scaleway-billing";

    /// <summary>Scaleway's header of a secret key.</summary>
    public const string TokenHeader = "X-Auth-Token";

    /// <summary>Scaleway's name of the Object Storage category, as the query filters it.</summary>
    public const string ObjectStorage = "Object Storage";

    private const string ConsumptionsPath = "/billing/v2beta1/consumptions";
    private const int PageSize = 100;

    // Two projects of a few SKUs fill a page: far beyond, the answer is not the bill expected.
    private const int MaxPages = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Every Object Storage line of the organization over <paramref name="period"/>.
    /// </summary>
    /// <exception cref="ScalewayBillingException">
    /// An error status or an unexpected answer.
    /// </exception>
    /// <exception cref="HttpRequestException">Scaleway is out of reach.</exception>
    /// <exception cref="TaskCanceledException">
    /// Scaleway did not answer in time, or <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public async Task<PeriodConsumptions> ReadPeriodAsync(
        string period,
        CancellationToken cancellationToken)
    {
        var lines = new List<ConsumptionLine>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var answer = await ReadPageAsync(period, page, cancellationToken);
            var consumptions = answer.Consumptions
                ?? throw new ScalewayBillingException($"Scaleway answered no list for {period}.");
            lines.AddRange(consumptions);
            if (consumptions.Count == 0 || lines.Count >= answer.TotalCount)
            {
                return new PeriodConsumptions(period, lines);
            }
        }

        throw new ScalewayBillingException(
            $"The bill of {period} runs past {MaxPages} pages of {PageSize} lines.");
    }

    private async Task<ConsumptionPage> ReadPageAsync(
        string period,
        int page,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, PathOf(period, page));
        request.Headers.TryAddWithoutValidation(TokenHeader, options.Value.SecretKey);
        using var response = await clients.CreateClient(ClientName)
            .SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ScalewayBillingException(
                $"Scaleway answered {(int)response.StatusCode} for the bill of {period}.");
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ConsumptionPage>(
                    Json,
                    cancellationToken)
                ?? throw new ScalewayBillingException($"Scaleway answered null for {period}.");
        }
        catch (JsonException exception)
        {
            throw new ScalewayBillingException(
                $"Scaleway's answer for {period} is not a list of consumptions.",
                exception);
        }
    }

    private string PathOf(string period, int page)
    {
        var organization = Guid.Parse(options.Value.OrganizationId!).ToString("D");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ConsumptionsPath}?organization_id={organization}"
                + $"&billing_period={Uri.EscapeDataString(period)}"
                + $"&category_name={Uri.EscapeDataString(ObjectStorage)}"
                + $"&page={page}&page_size={PageSize}");
    }
}
