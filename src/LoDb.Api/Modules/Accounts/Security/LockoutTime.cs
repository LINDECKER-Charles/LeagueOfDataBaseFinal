using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>How long a locked account stays locked: the <c>Retry-After</c> of its refusal.</summary>
internal static class LockoutTime
{
    /// <summary>
    /// The time left until <paramref name="user"/> may try again, within one lockout: the
    /// whole lockout when Identity dated none.
    /// </summary>
    public static TimeSpan Left(User user, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(clock);
        var longest = AccountsSecurityRegistration.LockoutDuration;
        return user.LockoutEnd is { } end
            ? TimeSpan.FromTicks(Math.Clamp((end - clock.GetUtcNow()).Ticks, 0, longest.Ticks))
            : longest;
    }
}
