using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Prerender.Revision;

internal sealed class PrerenderRevisionConfiguration
    : IEntityTypeConfiguration<PrerenderRevisionPage>
{
    private const int StateMaxLength = 16;

    public void Configure(EntityTypeBuilder<PrerenderRevisionPage> builder)
    {
        builder.ToTable("prerender_revision", static table =>
        {
            table.HasCheckConstraint(
                "ck_prerender_revision_state",
                "state IN ('pending', 'rendering', 'done', 'failed', 'skipped')");
            table.HasCheckConstraint("ck_prerender_revision_epoch", "epoch > 0");
            table.HasCheckConstraint("ck_prerender_revision_attempts", "attempts >= 0");
        });
        builder.HasKey(page => new { page.Path, page.Epoch });

        builder.Property(page => page.Path).HasMaxLength(PrerenderDemandPage.PathMaxLength);
        builder.Property(page => page.Version).HasMaxLength(PrerenderUnit.VersionMaxLength);
        builder.Property(page => page.Locale).HasMaxLength(PrerenderUnit.LocaleMaxLength);
        builder.Property(page => page.State)
            .HasMaxLength(StateMaxLength)
            .HasConversion(PrerenderDemandColumns.StateConverter);
        builder.Property(page => page.Attempts).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(page => page.EnqueuedAt).HasDefaultValueSql("now()");
        builder.Property(page => page.DueAt).HasDefaultValueSql("now()");
        builder.Property(page => page.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(page => page.Reason).HasMaxLength(PrerenderDemandPage.ReasonMaxLength);

        // The worker takes the pending pages that are due, the oldest first.
        builder.HasIndex(page => page.DueAt)
            .HasFilter("state = 'pending'")
            .HasDatabaseName("ix_prerender_revision_next");

        // The admin counts the pages written lately.
        builder.HasIndex(page => page.WrittenAt)
            .HasFilter("written_at IS NOT NULL")
            .HasDatabaseName("ix_prerender_revision_written");
    }
}
