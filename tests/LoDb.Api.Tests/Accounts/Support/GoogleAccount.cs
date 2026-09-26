namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>A Google account, as the userinfo endpoint (v3) describes it.</summary>
public sealed record GoogleAccount
{
    public required string Subject { get; init; }

    public required string Email { get; init; }

    public bool EmailVerified { get; init; } = true;

    public string? GivenName { get; init; }

    /// <summary>The JSON of the userinfo endpoint.</summary>
    public Dictionary<string, object> UserInfo()
    {
        var info = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = Subject,
            ["email"] = Email,
            ["email_verified"] = EmailVerified,
        };
        if (GivenName is not null)
        {
            info["given_name"] = GivenName;
            info["name"] = GivenName;
        }

        return info;
    }
}
