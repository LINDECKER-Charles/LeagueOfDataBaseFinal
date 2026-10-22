using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.States;

internal sealed class CatalogStateGenerationConfiguration
    : IEntityTypeConfiguration<CatalogStateGeneration>
{
    public void Configure(EntityTypeBuilder<CatalogStateGeneration> builder)
    {
        builder.ToTable("catalog_state_generation", static table =>
        {
            table.HasCheckConstraint("ck_catalog_state_generation_scheme", "scheme > 0");
            table.HasCheckConstraint(
                "ck_catalog_state_generation_counters",
                "state_count >= 0 AND open_state_count >= 0 AND open_state_count <= state_count");
        });
        builder.HasKey(generation => generation.Id);

        builder.Property(generation => generation.FirstVersion)
            .HasMaxLength(CatalogStateGeneration.VersionMaxLength);
        builder.Property(generation => generation.LastVersion)
            .HasMaxLength(CatalogStateGeneration.VersionMaxLength);
        builder.Property(generation => generation.BuiltAt).HasDefaultValueSql("now()");
        builder.Property(generation => generation.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(generation => generation.IsCurrent)
            .HasDefaultValue(false)
            .ValueGeneratedNever();
        builder.Property(generation => generation.StateCount)
            .HasDefaultValue(0)
            .ValueGeneratedNever();
        builder.Property(generation => generation.OpenStateCount)
            .HasDefaultValue(0)
            .ValueGeneratedNever();

        // One current generation per scheme: the flip of a rebuild clears the old one first,
        // in the same transaction, so a reader never sees two nor none.
        builder.HasIndex(generation => generation.Scheme)
            .IsUnique()
            .HasFilter("is_current")
            .HasDatabaseName("ux_catalog_state_generation_current");

        builder.HasMany<CatalogStateVersion>()
            .WithOne()
            .HasForeignKey(version => version.GenerationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<CatalogEntityState>()
            .WithOne()
            .HasForeignKey(state => state.GenerationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
