namespace LoDb.Desktop.Auth.Api;

/// <summary>Body of <c>POST /api/account/refresh</c>.</summary>
internal sealed record RefreshBody
{
    public required string RefreshToken { get; init; }
}
