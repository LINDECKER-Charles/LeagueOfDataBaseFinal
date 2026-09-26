namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>Body of <c>POST /api/account/reset-password</c>.</summary>
internal sealed record ResetPasswordRequest
{
    /// <summary>The <c>user</c> parameter of the link.</summary>
    public required int UserId { get; init; }

    /// <summary>The <c>token</c> parameter of the link, as it is.</summary>
    public required string? Token { get; init; }

    /// <summary>The new password, under the rules of a new account.</summary>
    public required string? Password { get; init; }
}
