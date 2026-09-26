namespace LoDb.Desktop.Auth.Api;

/// <summary>
/// A refusal of the API, kept as received so the token endpoints relay it unchanged: the
/// front reads the same ProblemDetails (and its <c>code</c>) as on the web.
/// </summary>
internal sealed record ApiProblem
{
    public required int Status { get; init; }

    public required string ContentType { get; init; }

    public required ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>The <c>code</c> extension of the ProblemDetails, when there is one.</summary>
    public string? Code { get; init; }
}
