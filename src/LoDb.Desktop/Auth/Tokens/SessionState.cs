namespace LoDb.Desktop.Auth.Tokens;

/// <summary>What the host holds, without a token: signed in, and kept across restarts.</summary>
internal sealed record SessionState
{
    public required bool IsSignedIn { get; init; }

    public required bool IsRemembered { get; init; }
}
