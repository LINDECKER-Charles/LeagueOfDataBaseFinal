namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>Body of <c>POST /api/account/reset-password/check</c>.</summary>
internal sealed record CheckResetTokenRequest
{
    /// <summary>The <c>user</c> parameter of the link.</summary>
    public required int UserId { get; init; }

    /// <summary>The <c>token</c> parameter of the link, as it is.</summary>
    public required string? Token { get; init; }
}
