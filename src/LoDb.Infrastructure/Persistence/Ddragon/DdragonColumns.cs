using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>Stored spelling of the lot 1 statuses, shared by the model and the raw SQL.</summary>
internal static class DdragonColumns
{
    public static readonly ValueConverter<DdragonAssetStatus, string> AssetStatusConverter = new(
        status => ToText(status),
        value => ParseAssetStatus(value));

    public static readonly ValueConverter<DdragonVersionStatus, string> VersionStatusConverter =
        new(status => ToText(status), value => ParseVersionStatus(value));

    public static string ToText(DdragonAssetStatus status) => status switch
    {
        DdragonAssetStatus.Present => "present",
        DdragonAssetStatus.Absent => "absent",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static DdragonAssetStatus ParseAssetStatus(string value) => value switch
    {
        "present" => DdragonAssetStatus.Present,
        "absent" => DdragonAssetStatus.Absent,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToText(DdragonVersionStatus status) => status switch
    {
        DdragonVersionStatus.Discovered => "discovered",
        DdragonVersionStatus.Ingesting => "ingesting",
        DdragonVersionStatus.Ready => "ready",
        DdragonVersionStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static DdragonVersionStatus ParseVersionStatus(string value) => value switch
    {
        "discovered" => DdragonVersionStatus.Discovered,
        "ingesting" => DdragonVersionStatus.Ingesting,
        "ready" => DdragonVersionStatus.Ready,
        "failed" => DdragonVersionStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
