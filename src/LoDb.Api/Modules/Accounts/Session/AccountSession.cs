namespace LoDb.Api.Modules.Accounts.Session;

/// <summary>
/// The session of the caller: its account, or none. The sign-in and the registration answer
/// it too, so that a front needs no second call.
/// </summary>
internal sealed record AccountSession
{
    public static AccountSession Anonymous { get; } = new() { User = null };

    /// <summary>The signed-in account; null for a visitor.</summary>
    public required AccountUser? User { get; init; }
}
