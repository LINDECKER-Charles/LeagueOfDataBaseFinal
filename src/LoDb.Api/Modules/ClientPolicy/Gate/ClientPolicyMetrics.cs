using System.Diagnostics.Metrics;

namespace LoDb.Api.Modules.ClientPolicy.Gate;

/// <summary>
/// Meter <c>LoDb.ClientPolicy</c>: the requests refused with a 426, by app, which tells how
/// many installations a new minimum still leaves behind.
/// </summary>
internal sealed class ClientPolicyMetrics
{
    public const string MeterName = "LoDb.ClientPolicy";
    public const string PlatformTag = "platform";

    private readonly Counter<long> _upgradeRequired;

    public ClientPolicyMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _upgradeRequired = meterFactory.Create(MeterName).CreateCounter<long>(
            "lodb.client_policy.upgrade_required",
            unit: "{response}",
            description: "Responses 426 Upgrade Required, by platform of the app.");
    }

    public void RecordUpgradeRequired(ClientPlatform platform) =>
        _upgradeRequired.Add(
            1,
            new KeyValuePair<string, object?>(PlatformTag, ClientPlatforms.NameOf(platform)));
}
