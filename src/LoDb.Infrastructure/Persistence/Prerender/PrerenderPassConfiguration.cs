using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Prerender;

internal sealed class PrerenderPassConfiguration : IEntityTypeConfiguration<PrerenderPass>
{
    private const int StateMaxLength = 16;
    private const string ActiveStates = "state IN ('running', 'paused')";

    public void Configure(EntityTypeBuilder<PrerenderPass> builder)
    {
        builder.ToTable("prerender_pass", ConfigureChecks);
        builder.HasKey(pass => pass.Id);
        ConfigureColumns(builder);

        // A unique index over a column that is true for every active pass and absent from the
        // others refuses a second active pass, whatever the epoch or the perimeter.
        builder.HasIndex(pass => pass.IsActive)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_prerender_pass_active");

        // The admin reads the history newest first.
        builder.HasIndex(pass => pass.CreatedAt).HasDatabaseName("ix_prerender_pass_created_at");

        builder.HasMany(pass => pass.Units)
            .WithOne(unit => unit.Pass)
            .HasForeignKey(unit => unit.PassId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureChecks(TableBuilder<PrerenderPass> table)
    {
        table.HasCheckConstraint(
            "ck_prerender_pass_state",
            "state IN ('running', 'paused', 'completed', 'cancelled')");
        table.HasCheckConstraint("ck_prerender_pass_epoch", "epoch > 0");
        table.HasCheckConstraint("ck_prerender_pass_scope", "jsonb_typeof(scope) = 'object'");
        table.HasCheckConstraint(
            "ck_prerender_pass_counters",
            "pages_total >= 0 AND pages_written >= 0 AND pages_skipped >= 0"
                + " AND pages_failed >= 0 AND bytes_written >= 0 AND active_seconds >= 0");
        table.HasCheckConstraint(
            "ck_prerender_pass_finished",
            "(finished_at IS NOT NULL) = (state IN ('completed', 'cancelled'))");
        table.HasCheckConstraint(
            "ck_prerender_pass_running",
            "(running_since IS NOT NULL) = (state = 'running')");
    }

    private static void ConfigureColumns(EntityTypeBuilder<PrerenderPass> builder)
    {
        builder.Property(pass => pass.State)
            .HasMaxLength(StateMaxLength)
            .HasConversion(PrerenderColumns.PassStateConverter);
        builder.Property(pass => pass.PagesTotal).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(pass => pass.PagesWritten).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(pass => pass.PagesSkipped).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(pass => pass.PagesFailed).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(pass => pass.BytesWritten).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(pass => pass.ActiveSeconds).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(pass => pass.Scope)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");
        builder.Property(pass => pass.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(pass => pass.PauseReason).HasMaxLength(PrerenderPass.PauseReasonMaxLength);
        builder.Property(pass => pass.IsActive)
            .HasComputedColumnSql(ActiveStates, stored: true);
    }
}
