using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Prerender.Pace;

internal sealed class PrerenderPaceSettingConfiguration
    : IEntityTypeConfiguration<PrerenderPaceSetting>
{
    public void Configure(EntityTypeBuilder<PrerenderPaceSetting> builder)
    {
        builder.ToTable("prerender_pace_setting", static table =>
        {
            // One row at most: the setting of the stack, never one per instance.
            table.HasCheckConstraint("ck_prerender_pace_setting_singleton", "id = 1");
            table.HasCheckConstraint(
                "ck_prerender_pace_setting_mode",
                "mode IN ('adaptive', 'manual')");
            table.HasCheckConstraint(
                "ck_prerender_pace_setting_rate",
                "renders_per_minute > 0");
        });
        builder.HasKey(setting => setting.Id);

        builder.Property(setting => setting.Id).ValueGeneratedNever();
        builder.Property(setting => setting.Mode)
            .HasMaxLength(PrerenderPaceSetting.ModeMaxLength);
        builder.Property(setting => setting.UpdatedAt).HasDefaultValueSql("now()");
    }
}
