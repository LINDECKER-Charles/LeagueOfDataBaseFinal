using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Accounts.Google;

/// <summary>The account a Google identity maps to, or why none does.</summary>
internal sealed record GoogleProvisioning
{
    public User? Account { get; private init; }

    /// <summary>One of <see cref="GoogleFailures"/>, when no account is found or created.</summary>
    public string? Failure { get; private init; }

    public static GoogleProvisioning Of(User account) => new() { Account = account };

    public static GoogleProvisioning Failed(string failure) => new() { Failure = failure };
}
