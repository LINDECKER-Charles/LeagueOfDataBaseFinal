using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Prerender.Demand;

internal sealed class PrerenderDemandConfiguration : IEntityTypeConfiguration<PrerenderDemandPage>
{
    private const int StateMaxLength = 16;

    public void Configure(EntityTypeBuilder<PrerenderDemandPage> builder)
    {
        builder.ToTable("prerender_demand", static table =>
        {
            table.HasCheckConstraint(
                "ck_prerender_demand_state",
                "state IN ('pending', 'rendering', 'done', 'failed', 'skipped')");
            table.HasCheckConstraint(
                "ck_prerender_demand_counters",
                "demand >= 0 AND ssr_count >= 0 AND attempts >= 0");
        });
        builder.HasKey(page => page.Path);

        builder.Property(page => page.Path).HasMaxLength(PrerenderDemandPage.PathMaxLength);
        builder.Property(page => page.Version).HasMaxLength(PrerenderUnit.VersionMaxLength);
        builder.Property(page => page.Locale).HasMaxLength(PrerenderUnit.LocaleMaxLength);
        builder.Property(page => page.PageType)
            .HasMaxLength(PrerenderDemandPage.PageTypeMaxLength);
        builder.Property(page => page.SsrInstance)
            .HasMaxLength(PrerenderDemandPage.InstanceMaxLength);
        builder.Property(page => page.State)
            .HasMaxLength(StateMaxLength)
            .HasConversion(PrerenderDemandColumns.StateConverter);
        builder.Property(page => page.Demand).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(page => page.SsrCount).HasDefaultValue(0L).ValueGeneratedNever();
        builder.Property(page => page.Attempts).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(page => page.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(page => page.Reason).HasMaxLength(PrerenderDemandPage.ReasonMaxLength);
        builder.Property(page => page.StoredClass)
            .HasMaxLength(PrerenderDemandPage.StoredClassMaxLength);

        // The worker takes the most requested pending pages, and the admin the first ten: a
        // partial index, as small as the queue itself.
        builder.HasIndex(page => page.Demand)
            .IsDescending()
            .HasFilter("state = 'pending'")
            .HasDatabaseName("ix_prerender_demand_next");

        // The admin counts the pages written in the last hour and day.
        builder.HasIndex(page => page.RenderedAt)
            .HasFilter("rendered_at IS NOT NULL")
            .HasDatabaseName("ix_prerender_demand_rendered");

        // The admin counts and weighs the pages the queue holds in each epoch, at every read
        // of the report: the done rows no pass wrote again, read from the index alone.
        builder.HasIndex(page => page.Epoch)
            .IncludeProperties(page => page.Bytes)
            .HasFilter("state = 'done' AND superseded_at IS NULL")
            .HasDatabaseName("ix_prerender_demand_stored");
    }
}
