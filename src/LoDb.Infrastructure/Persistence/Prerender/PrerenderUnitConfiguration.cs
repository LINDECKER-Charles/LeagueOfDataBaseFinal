using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Prerender;

internal sealed class PrerenderUnitConfiguration : IEntityTypeConfiguration<PrerenderUnit>
{
    private const int StateMaxLength = 16;

    public void Configure(EntityTypeBuilder<PrerenderUnit> builder)
    {
        builder.ToTable("prerender_unit", static table =>
        {
            table.HasCheckConstraint(
                "ck_prerender_unit_state",
                "state IN ('pending', 'running', 'completed', 'failed')");
            table.HasCheckConstraint(
                "ck_prerender_unit_counters",
                "pages_total >= 0 AND pages_written >= 0 AND pages_skipped >= 0"
                    + " AND pages_failed >= 0 AND bytes_written >= 0 AND cursor >= 0");
        });
        builder.HasKey(unit => new { unit.PassId, unit.Version, unit.Locale });

        builder.Property(unit => unit.Version).HasMaxLength(PrerenderUnit.VersionMaxLength);
        builder.Property(unit => unit.Locale).HasMaxLength(PrerenderUnit.LocaleMaxLength);
        builder.Property(unit => unit.State)
            .HasMaxLength(StateMaxLength)
            .HasConversion(PrerenderColumns.UnitStateConverter);
        builder.Property(unit => unit.Priority).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(unit => unit.PagesTotal).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(unit => unit.PagesWritten).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(unit => unit.PagesSkipped).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(unit => unit.PagesFailed).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(unit => unit.BytesWritten).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(unit => unit.Cursor).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(unit => unit.UpdatedAt).HasDefaultValueSql("now()");

        // The worker takes the next unit of a pass by state, then priority.
        builder.HasIndex(unit => new { unit.PassId, unit.State, unit.Priority })
            .HasDatabaseName("ix_prerender_unit_next");
    }
}
