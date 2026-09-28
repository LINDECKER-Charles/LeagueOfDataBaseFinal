namespace LoDb.Desktop.Auth.Google;

/// <summary>A Google sign-in waiting for its loopback redirect.</summary>
internal sealed record GoogleFlow
{
    /// <summary>The OAuth state: the callback is accepted for this value only, once.</summary>
    public required string State { get; init; }

    /// <summary>The PKCE verifier, sent to the API with the code, never to the browser.</summary>
    public required string Verifier { get; init; }

    /// <summary>The loopback redirect URI, which the exchange repeats exactly.</summary>
    public required string RedirectUri { get; init; }

    /// <summary>"Remember me" as asked when the flow began.</summary>
    public required bool IsRemembered { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
