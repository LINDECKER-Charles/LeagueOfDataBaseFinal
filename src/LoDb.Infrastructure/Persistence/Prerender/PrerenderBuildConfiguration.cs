using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Prerender;

internal sealed class PrerenderBuildConfiguration : IEntityTypeConfiguration<PrerenderBuild>
{
    public void Configure(EntityTypeBuilder<PrerenderBuild> builder)
    {
        builder.ToTable("prerender_build", static table =>
            table.HasCheckConstraint("ck_prerender_build_hash", "hash ~ '^[0-9a-f]{64}$'"));
        builder.HasKey(build => build.Hash);

        builder.Property(build => build.Hash)
            .HasMaxLength(PrerenderBuild.HashLength)
            .IsFixedLength();
        builder.Property(build => build.StoredAt).HasDefaultValueSql("now()");
    }
}
