using System.Net.Http.Headers;
using System.Text.Json;

namespace LoDb.Desktop.Auth.Api;

/// <summary>
/// The host's own calls to the token endpoints of the API (L4.2). These never go through
/// the proxy: their answers carry tokens, which must not reach the page.
/// </summary>
internal sealed partial class AccountApiClient(
    IHttpClientFactory clients,
    ILogger<AccountApiClient> logger)
{
    /// <summary>The named client, configured with the API origin by the auth services.</summary>
    public const string ClientName = "lodb-api";

    private const string TokenPath = "/api/account/token";
    private const string RefreshPath = "/api/account/refresh";
    private const string GoogleExchangePath = "/api/account/google/app/exchange";
    private const string ProblemCodeMember = "code";
    private const string FallbackContentType = "application/problem+json";

    public Task<AccountCall> SignInAsync(
        PasswordSignInBody body,
        CancellationToken cancellationToken) =>
        PostAsync(TokenPath, body, cancellationToken);

    public Task<AccountCall> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken) =>
        PostAsync(RefreshPath, new RefreshBody { RefreshToken = refreshToken }, cancellationToken);

    public Task<AccountCall> ExchangeGoogleAsync(
        GoogleExchangeBody body,
        CancellationToken cancellationToken) =>
        PostAsync(GoogleExchangePath, body, cancellationToken);

    private async Task<AccountCall> PostAsync<TBody>(
        string path,
        TBody body,
        CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient(ClientName);
        try
        {
            using var response = await client.PostAsJsonAsync(path, body, cancellationToken);
            return response.IsSuccessStatusCode
                ? await ReadGrantAsync(response, cancellationToken)
                : await ReadProblemAsync(response, cancellationToken);
        }
        catch (Exception exception) when (IsUnreachable(exception, cancellationToken))
        {
            LogUnreachable(logger, path, exception);
            return AccountCall.Unreachable;
        }
    }

    // A timeout surfaces as a cancellation that the caller did not ask for.
    private static bool IsUnreachable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or JsonException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static async Task<AccountCall> ReadGrantAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var grant = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return grant is { IsComplete: true }
            ? new AccountCall { Grant = grant }
            : throw new JsonException("The token response lacks a token or a lifetime.");
    }

    private static async Task<AccountCall> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new AccountCall
        {
            Problem = new ApiProblem
            {
                Status = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString()
                    ?? FallbackContentType,
                Body = body,
                Code = ReadCode(body, response.Content.Headers.ContentType),
            },
        };
    }

    private static string? ReadCode(byte[] body, MediaTypeHeaderValue? contentType)
    {
        if (contentType?.MediaType?.EndsWith("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        try
        {
            using var problem = JsonDocument.Parse(body);
            return problem.RootElement.ValueKind == JsonValueKind.Object
                && problem.RootElement.TryGetProperty(ProblemCodeMember, out var code)
                && code.ValueKind == JsonValueKind.String
                    ? code.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(
        EventName = "desktop.api.unreachable",
        Level = LogLevel.Warning,
        Message = "The API could not be reached for {Path}.")]
    private static partial void LogUnreachable(ILogger logger, string path, Exception exception);
}
