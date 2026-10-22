using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Pace;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

/// <summary>
/// Reads and writes the single row of <c>prerender_pace_setting</c>. Absent, the setting is
/// the default (<see cref="PaceSetting.DefaultOf"/>), which the caller supplies.
/// </summary>
internal sealed class PaceSettingStore(
    IDbContextFactory<LoDbDbContext> contexts,
    TimeProvider clock) : IPaceSettingSource
{
    /// <summary>The stored setting; null while none was ever stored.</summary>
    public async Task<PaceSetting?> ReadAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.PrerenderPaceSettings.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null || PaceSettingText.Parse(row.Mode) is not { } mode)
        {
            return null;
        }

        return new PaceSetting(mode, row.RendersPerMinute) { UpdatedAt = row.UpdatedAt };
    }

    /// <summary>
    /// Stores <paramref name="setting"/> in one statement, so that two operators setting it
    /// at once leave one of their choices, never an error.
    /// </summary>
    public async Task SaveAsync(PaceSetting setting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setting);
        var (mode, rate, at) = (PaceSettingText.Of(setting.Mode), setting.RendersPerMinute,
            clock.GetUtcNow());
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO prerender_pace_setting (id, mode, renders_per_minute, updated_at)
            VALUES ({PrerenderPaceSetting.SingletonId}, {mode}, {rate}, {at})
            ON CONFLICT (id) DO UPDATE SET mode = excluded.mode,
                renders_per_minute = excluded.renders_per_minute,
                updated_at = excluded.updated_at
            """,
            cancellationToken);
    }
}
