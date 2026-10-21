namespace LoDb.Api.Modules.Admin.Prerender.Units.Waits;

/// <summary>A version whose units wait in a pass, why, and since when (wall clock).</summary>
internal sealed record UnitWait(string Version, UnitWaitReason Reason, DateTimeOffset Since)
{
    /// <summary>How the admin spells <paramref name="reason"/>.</summary>
    public static string TextOf(UnitWaitReason reason) => reason switch
    {
        UnitWaitReason.CatalogPending => "catalog-pending",
        UnitWaitReason.NotPinned => "not-pinned",
        UnitWaitReason.Settling => "settling",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };
}
