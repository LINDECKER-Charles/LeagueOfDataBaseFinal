using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Modules.Billing.Subscriptions;

/// <summary>What a plan gives a key: its code, its monthly quota and its rate.</summary>
internal sealed record KeyRights(string Plan, int MonthlyQuota, int RatePerMinute)
{
    public static KeyRights Free { get; } =
        new(ApiPlans.Free, ApiPlans.FreeQuota, ApiPlans.FreeRate);

    public static KeyRights Of(ApiPlanTerms plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new KeyRights(plan.Code, plan.MonthlyQuota, plan.RatePerMinute);
    }

    /// <summary>
    /// Whether <paramref name="key"/> holds these rights already, its rate at the credits floor
    /// while it holds credits.
    /// </summary>
    public bool AreHeldBy(ApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Plan == Plan
            && key.MonthlyQuota == MonthlyQuota
            && key.RateLimitPerMin == RateFor(key);
    }

    /// <summary>Gives these rights to <paramref name="key"/>, tracked by the context.</summary>
    public void GrantTo(ApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        key.Plan = Plan;
        key.MonthlyQuota = MonthlyQuota;
        key.RateLimitPerMin = RateFor(key);
    }

    private int RateFor(ApiKey key) => ApiPlans.RateOf(RatePerMinute, key.CreditsBalance);
}
