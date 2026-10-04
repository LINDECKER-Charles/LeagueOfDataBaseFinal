namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>Body of <c>POST /api/account/verify-email</c>: the parameters of the link.</summary>
internal sealed record VerifyEmailRequest
{
    /// <summary>The <c>user</c> parameter of the link.</summary>
    public required int UserId { get; init; }

    /// <summary>The <c>token</c> parameter of the link, as it is.</summary>
    public required string? Token { get; init; }
}
