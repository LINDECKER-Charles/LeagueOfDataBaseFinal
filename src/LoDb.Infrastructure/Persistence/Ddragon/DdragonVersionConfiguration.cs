using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Ddragon;

internal sealed class DdragonVersionConfiguration : IEntityTypeConfiguration<DdragonVersion>
{
    public void Configure(EntityTypeBuilder<DdragonVersion> builder)
    {
        builder.ToTable("ddragon_version", static table =>
        {
            table.HasCheckConstraint(
                "ck_ddragon_version_status",
                "status IN ('discovered', 'ingesting', 'ready', 'failed')");
            table.HasCheckConstraint("ck_ddragon_version_attempts", "attempts >= 0");
        });
        builder.HasKey(version => version.Version);

        builder.Property(version => version.Version).HasMaxLength(32);
        builder.Property(version => version.Status)
            .HasMaxLength(16)
            .HasConversion(DdragonColumns.VersionStatusConverter);
        builder.Property(version => version.Attempts).HasDefaultValue(0).ValueGeneratedNever();
    }
}
