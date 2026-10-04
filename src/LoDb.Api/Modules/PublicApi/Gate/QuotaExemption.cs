namespace LoDb.Api.Modules.PublicApi.Gate;

/// <summary>
/// Endpoint metadata of a route of <c>/v1</c> the quota neither charges nor meters:
/// <c>/v1/usage</c> stays readable once the quota is spent.
/// </summary>
internal sealed class QuotaExemption
{
    private QuotaExemption()
    {
    }

    public static QuotaExemption Instance { get; } = new();
}
