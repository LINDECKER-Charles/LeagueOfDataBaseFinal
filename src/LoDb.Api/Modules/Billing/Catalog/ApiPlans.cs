namespace LoDb.Api.Modules.Billing.Catalog;

/// <summary>
/// The rights a key holds by plan, the legacy <c>ApiPlan</c>: the free plan, the rate floor of
/// a key holding credits, and the four subscriptions.
/// </summary>
internal static class ApiPlans
{
    public const string Free = "free";
    public const int FreeQuota = 500;
    public const int FreeRate = 10;

    /// <summary>The least rate of a key holding credits (<c>RATE_CREDITS</c>).</summary>
    public const int CreditsRate = 60;

    private const string Month = "month";
    private const string Year = "year";

    /// <summary>The plans sold as subscriptions, from the cheapest.</summary>
    public static IReadOnlyList<ApiPlanTerms> Subscriptions { get; } =
    [
        new()
        {
            Code = "monthly", MonthlyQuota = 15_000, RatePerMinute = 120,
            PriceCents = 500, Interval = Month,
        },
        new()
        {
            Code = "monthly_plus", MonthlyQuota = 45_000, RatePerMinute = 120,
            PriceCents = 1_500, Interval = Month,
        },
        new()
        {
            Code = "annual", MonthlyQuota = 20_000, RatePerMinute = 300,
            PriceCents = 4_800, Interval = Year,
        },
        new()
        {
            Code = "annual_plus", MonthlyQuota = 60_000, RatePerMinute = 300,
            PriceCents = 14_400, Interval = Year,
        },
    ];

    /// <summary>The subscription named <paramref name="code"/>; null for any other code.</summary>
    public static ApiPlanTerms? Find(string? code) =>
        Subscriptions.FirstOrDefault(plan => plan.Code.Equals(code, StringComparison.Ordinal));

    /// <summary>
    /// The rate of a key on a plan of <paramref name="planRate"/>: never below
    /// <see cref="CreditsRate"/> while it holds credits.
    /// </summary>
    public static int RateOf(int planRate, long creditsBalance) =>
        creditsBalance > 0 ? Math.Max(planRate, CreditsRate) : planRate;
}
