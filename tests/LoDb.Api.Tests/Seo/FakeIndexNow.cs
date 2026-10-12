using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// Stands for the IndexNow endpoint: records every request, its body parsed, and accepts it
/// with a 200.
/// </summary>
public sealed class FakeIndexNow : HttpMessageHandler
{
    private readonly ConcurrentQueue<Submission> _submissions = new();

    public IReadOnlyList<Submission> Submissions => [.. _submissions];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? "{}"
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _submissions.Enqueue(new Submission(
            request.Method,
            request.RequestUri,
            request.Content?.Headers.ContentType?.MediaType,
            JsonDocument.Parse(body).RootElement.Clone()));
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    /// <summary>One request as the endpoint received it.</summary>
    public sealed record Submission(
        HttpMethod Method,
        Uri? Url,
        string? MediaType,
        JsonElement Body);
}
