namespace LoDb.Api.Modules.PublicApi.Keys.Contracts;

/// <summary>The API key of the signed-in account, if it has an active one.</summary>
internal sealed record ApiKeyState
{
    /// <summary>Null while the account has no active key.</summary>
    public required ApiKeyOverview? Key { get; init; }
}
