using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.States;

internal sealed class CatalogStateVersionConfiguration
    : IEntityTypeConfiguration<CatalogStateVersion>
{
    public void Configure(EntityTypeBuilder<CatalogStateVersion> builder)
    {
        builder.ToTable("catalog_state_version", static table =>
            table.HasCheckConstraint("ck_catalog_state_version_ordinal", "ordinal >= 0"));
        builder.HasKey(version => new { version.GenerationId, version.Version });

        builder.Property(version => version.Version)
            .HasMaxLength(CatalogStateGeneration.VersionMaxLength);
        builder.Property(version => version.IndexedAt).HasDefaultValueSql("now()");

        // The coverage is a run: one version per position.
        builder.HasIndex(version => new { version.GenerationId, version.Ordinal })
            .IsUnique()
            .HasDatabaseName("ux_catalog_state_version_ordinal");
    }
}
