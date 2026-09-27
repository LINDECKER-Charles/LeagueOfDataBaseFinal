namespace LoDb.Api.Modules.PublicApi.Keys.Reference;

/// <summary>The rights of a new key, before any purchase.</summary>
internal sealed record FreePlanTerms
{
    public required int MonthlyQuota { get; init; }

    public required int RatePerMinute { get; init; }
}
