using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// Google's token and userinfo endpoints behind the backchannel of the handler: the
/// <see cref="Code"/> is exchanged for an access token, which reads <see cref="Account"/>.
/// </summary>
public sealed class FakeGoogle : HttpMessageHandler
{
    /// <summary>The code Google hands to the callback, or to an app.</summary>
    public const string Code = "google-code";

    private const string AccessToken = "google-access-token";
    private const string TokenHost = "oauth2.googleapis.com";
    private const string CodeField = "code";
    private const int TokenLifetimeSeconds = 3599;

    private readonly ConcurrentQueue<IReadOnlyDictionary<string, string>> _exchanges = new();

    /// <summary>The Google account signing in; without one, userinfo answers 401.</summary>
    public GoogleAccount? Account { get; set; }

    /// <summary>Whether Google cannot be reached at all.</summary>
    public bool Unreachable { get; set; }

    /// <summary>The form of each code exchange, oldest first.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Exchanges => [.. _exchanges];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (Unreachable)
        {
            throw new HttpRequestException("Google cannot be reached.");
        }

        if (request.RequestUri?.Host == TokenHost)
        {
            return await ExchangeAsync(request, cancellationToken);
        }

        return request.Headers.Authorization?.Parameter == AccessToken && Account is { } account
            ? Json(HttpStatusCode.OK, account.UserInfo())
            : new HttpResponseMessage(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> ExchangeAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var form = await request.Content!.ReadAsStringAsync(cancellationToken);
        var fields = QueryHelpers.ParseQuery(form).ToDictionary(
            static field => field.Key,
            static field => field.Value.ToString(),
            StringComparer.Ordinal);
        _exchanges.Enqueue(fields);
        if (fields.GetValueOrDefault(CodeField) != Code)
        {
            return Json(
                HttpStatusCode.BadRequest,
                new Dictionary<string, object> { ["error"] = "invalid_grant" });
        }

        return Json(HttpStatusCode.OK, new Dictionary<string, object>
        {
            ["access_token"] = AccessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = TokenLifetimeSeconds,
        });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };
}
