using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.States;

internal sealed class CatalogEntityStateConfiguration
    : IEntityTypeConfiguration<CatalogEntityState>
{
    public void Configure(EntityTypeBuilder<CatalogEntityState> builder)
    {
        builder.ToTable("catalog_entity_state", ConfigureChecks);
        builder.HasKey(state => state.Id);
        ConfigureColumns(builder);

        // An entity opens one state per version at most, and has one open state at most.
        builder.HasIndex(state => new
            {
                state.GenerationId,
                state.Resource,
                state.EntityKey,
                state.OpenerVersion,
            })
            .IsUnique()
            .HasDatabaseName("ux_catalog_entity_state_opener");
        builder.HasIndex(state => new { state.GenerationId, state.Resource, state.EntityKey })
            .IsUnique()
            .HasFilter("closed_at IS NULL")
            .HasDatabaseName("ux_catalog_entity_state_open");

        // The re-render of closed states reads the closures past its watermark.
        builder.HasIndex(state => new { state.GenerationId, state.ClosedAt })
            .HasFilter("closed_at IS NOT NULL")
            .HasDatabaseName("ix_catalog_entity_state_closed");
    }

    private static void ConfigureChecks(TableBuilder<CatalogEntityState> table)
    {
        table.HasCheckConstraint(
            "ck_catalog_entity_state_resource",
            "resource IN ('champions', 'items', 'runes', 'summoners')");
        table.HasCheckConstraint(
            "ck_catalog_entity_state_closed",
            "(closed_at IS NULL) = (last_version IS NULL)");
        table.HasCheckConstraint(
            "ck_catalog_entity_state_return",
            "NOT is_return OR previous_opener_version IS NOT NULL");
        table.HasCheckConstraint(
            "ck_catalog_entity_state_new",
            "previous_opener_version IS NOT NULL OR cardinality(changed_fields) = 0");
        table.HasCheckConstraint(
            "ck_catalog_entity_state_fingerprint",
            "fingerprint ~ '^[0-9a-f]{64}$'");
    }

    private static void ConfigureColumns(EntityTypeBuilder<CatalogEntityState> builder)
    {
        builder.Property(state => state.Resource)
            .HasMaxLength(CatalogStateColumns.ResourceMaxLength)
            .HasConversion(CatalogStateColumns.ResourceConverter);
        builder.Property(state => state.EntityKey)
            .HasMaxLength(CatalogEntityState.EntityKeyMaxLength);
        builder.Property(state => state.OpenerVersion)
            .HasMaxLength(CatalogStateGeneration.VersionMaxLength);
        builder.Property(state => state.LastVersion)
            .HasMaxLength(CatalogStateGeneration.VersionMaxLength);
        builder.Property(state => state.PreviousOpenerVersion)
            .HasMaxLength(CatalogStateGeneration.VersionMaxLength);
        builder.Property(state => state.Fingerprint)
            .HasMaxLength(CatalogEntityState.FingerprintLength)
            .IsFixedLength();
        builder.Property(state => state.Path).HasMaxLength(CatalogEntityState.PathMaxLength);
        builder.Property(state => state.IsReturn).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(state => state.ChangedFields)
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]");
    }
}
