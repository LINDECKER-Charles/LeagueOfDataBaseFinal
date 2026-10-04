namespace LoDb.Api.Cli.Admin;

/// <summary>What <c>admin root</c> changed; nothing at all on most deployments.</summary>
internal sealed record AdminRootResult
{
    public required bool Created { get; init; }

    /// <summary>The password was set anew: its sessions are signed out.</summary>
    public required bool PasswordChanged { get; init; }

    public required bool Promoted { get; init; }
}
