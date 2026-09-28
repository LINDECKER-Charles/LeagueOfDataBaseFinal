using System.Net;
using System.Text.Json;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>Reads the JSON answers of the accounts API, their status checked first.</summary>
public static class ApiJson
{
    public const string ProblemMediaType = "application/problem+json";

    private const string ValidationFailed = "validation-failed";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>The body of an answer, which must have <paramref name="status"/>.</summary>
    public static async Task<JsonElement> ReadAsync(
        HttpResponseMessage response,
        HttpStatusCode status)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.True(response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    /// <summary>The <c>code</c> of a problem answered with <paramref name="status"/>.</summary>
    public static async Task<string?> ProblemCodeAsync(
        HttpResponseMessage response,
        HttpStatusCode status)
    {
        var problem = await ReadAsync(response, status);
        Assert.Equal(ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
        return problem.GetProperty("code").GetString();
    }

    /// <summary>The codes of a validation problem, by field.</summary>
    public static async Task<IReadOnlyDictionary<string, string[]>> FieldErrorsAsync(
        HttpResponseMessage response)
    {
        var problem = await ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(ValidationFailed, problem.GetProperty("code").GetString());
        return problem.GetProperty("errors").EnumerateObject().ToDictionary(
            static field => field.Name,
            static field => field.Value.EnumerateArray()
                .Select(static code => code.GetString()!)
                .ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>A string property of <paramref name="element"/>.</summary>
    public static string? Text(JsonElement element, string name) =>
        element.GetProperty(name).GetString();
}
