using LoDb.Api.Modules.Admin.Http;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Analytics.Reports;

namespace LoDb.Api.Modules.Analytics.Admin;

/// <summary>
/// The <c>?range=</c> of the analytics reports: <c>7d</c>, <c>30d</c> (by default),
/// <c>90d</c> or <c>all</c>, refused as <see cref="InvalidRange"/> otherwise.
/// </summary>
internal static class RangeParameter
{
    /// <summary>The period is none of <see cref="AnalyticsRange.Names"/>.</summary>
    public const string InvalidRange = "invalid-range";

    private const string RangeKey = "range";

    /// <returns>The period; null when the name is none of the known ones.</returns>
    public static AnalyticsRange? Read(string? range) =>
        AnalyticsRange.TryParse(range ?? AnalyticsRange.Default, out var period) ? period : null;

    public static AccountProblem Invalid() => AdminProblems.Invalid(RangeKey, InvalidRange);
}
