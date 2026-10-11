using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security.Limits;

/// <summary>
/// Registers the hourly limit of a costly action of a signed-in account: a checkout opened,
/// an API key issued, a build saved, a vote cast. The module that owns the action calls it
/// once, and its endpoints require <see cref="AccountActionLimit.Policy"/>.
/// </summary>
/// <remarks>
/// <para>
/// Counted by account, never by address (<see cref="AccountPartitions"/>): an authenticated
/// action is never limited per address, so that members behind one NAT keep their own quota.
/// A refusal is the host's 429 <c>rate-limited</c>, in the memory of each instance.
/// </para>
/// <para>
/// No I/O and nothing thrown here: a bound out of range stops the host when it starts.
/// </para>
/// </remarks>
internal static class AccountActionLimits
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    public static IServiceCollection AddAccountActionLimit(
        this IServiceCollection services,
        AccountActionLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        var key = $"{SignInLimitOptions.SectionName}:{limit.Setting}";
        services.AddOptions<AccountActionLimitOptions>(limit.Policy)
            .Configure<IConfiguration>((options, configuration) =>
                options.PerHour = configuration.GetValue(key, limit.DefaultPerHour))
            .Validate(
                static options => options.IsUsable(),
                $"{key} must lie between 1 and {AccountActionLimitOptions.MostPerHour}.")
            .ValidateOnStart();
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptionsMonitor<AccountActionLimitOptions>>((options, bounds) =>
            {
                var perHour = bounds.Get(limit.Policy).PerHour;
                options.AddPolicy(
                    limit.Policy,
                    context => AccountPartitions.OfAccount(context, perHour, Hour));
            });
        return services;
    }
}
