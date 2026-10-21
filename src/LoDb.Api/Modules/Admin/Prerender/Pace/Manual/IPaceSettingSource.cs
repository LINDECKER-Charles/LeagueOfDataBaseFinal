namespace LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

/// <summary>Where the sampler reads the stored setting (<see cref="PaceSettingStore"/>).</summary>
internal interface IPaceSettingSource
{
    /// <summary>The stored setting; null while none was ever stored.</summary>
    Task<PaceSetting?> ReadAsync(CancellationToken cancellationToken);
}
