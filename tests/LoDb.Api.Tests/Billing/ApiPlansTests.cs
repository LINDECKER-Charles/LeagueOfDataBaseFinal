using LoDb.Api.Modules.Billing.Catalog;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// The quota and rate a subscription gives its key, as go-api's plans gave them; the key
/// takes them as they are when the plan is paid.
/// </summary>
public sealed class ApiPlansTests
{
    [Theory]
    [InlineData("monthly_plus", 45_000, 120)]
    [InlineData("annual", 20_000, 300)]
    [InlineData("annual_plus", 60_000, 300)]
    public void PlanGivesItsQuotaAndRate(string code, int quota, int rate)
    {
        var plan = ApiPlans.Find(code)!;

        Assert.Equal((quota, rate), (plan.MonthlyQuota, plan.RatePerMinute));
    }
}
