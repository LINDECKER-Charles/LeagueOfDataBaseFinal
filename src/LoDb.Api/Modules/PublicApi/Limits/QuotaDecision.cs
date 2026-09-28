namespace LoDb.Api.Modules.PublicApi.Limits;

/// <summary>How a billed request is paid for.</summary>
internal enum QuotaDecision
{
    /// <summary>The monthly quota covers it: counted already.</summary>
    Plan,

    /// <summary>The quota is spent, credits may be left: one must be taken first.</summary>
    NeedsCredit,

    /// <summary>Neither the quota nor the credits: <c>429 quota_exceeded</c>.</summary>
    Denied,
}
