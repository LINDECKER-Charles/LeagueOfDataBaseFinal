namespace LoDb.Api.Hosting.Health;

/// <summary>
/// Body of the health probes: the overall status and the status of each check.
/// </summary>
/// <remarks>
/// Descriptions and exceptions stay out: the probes are anonymous, and an exception text
/// may name internal hosts.
/// </remarks>
internal sealed record HealthResponse
{
    public required string Status { get; init; }

    public required IReadOnlyDictionary<string, string> Checks { get; init; }
}
