using Microsoft.AspNetCore.Mvc;

namespace LoDb.Desktop.Auth.Google;

/// <summary>The query of Google's redirect to the loopback callback.</summary>
internal sealed record GoogleCallbackQuery
{
    [FromQuery(Name = "code")]
    public string? Code { get; init; }

    [FromQuery(Name = "state")]
    public string? State { get; init; }

    /// <summary><c>access_denied</c> when the user declined, another code otherwise.</summary>
    [FromQuery(Name = "error")]
    public string? Error { get; init; }
}
