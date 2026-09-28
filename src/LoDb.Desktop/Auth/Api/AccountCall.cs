namespace LoDb.Desktop.Auth.Api;

/// <summary>
/// Result of a token call: a grant, a refusal of the API, or neither when the API could
/// not be reached (network, timeout, unreadable answer).
/// </summary>
internal sealed record AccountCall
{
    public static readonly AccountCall Unreachable = new();

    public TokenResponse? Grant { get; init; }

    public ApiProblem? Problem { get; init; }
}
