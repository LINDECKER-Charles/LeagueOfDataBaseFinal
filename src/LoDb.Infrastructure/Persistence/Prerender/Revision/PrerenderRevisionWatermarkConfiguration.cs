using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Prerender.Revision;

internal sealed class PrerenderRevisionWatermarkConfiguration
    : IEntityTypeConfiguration<PrerenderRevisionWatermark>
{
    public void Configure(EntityTypeBuilder<PrerenderRevisionWatermark> builder)
    {
        // One row at most: the watermark of the stack, never one per instance.
        builder.ToTable("prerender_revision_watermark", static table =>
            table.HasCheckConstraint("ck_prerender_revision_watermark_singleton", "id = 1"));
        builder.HasKey(watermark => watermark.Id);

        builder.Property(watermark => watermark.Id).ValueGeneratedNever();
        builder.Property(watermark => watermark.UpdatedAt).HasDefaultValueSql("now()");
    }
}
