using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Builds;

internal sealed class BuildConfiguration : IEntityTypeConfiguration<Build>
{
    public void Configure(EntityTypeBuilder<Build> builder)
    {
        builder.ToTable("builds");
        builder.HasKey(build => build.Id).HasName("builds_pkey");

        builder.Property(build => build.Name).HasMaxLength(80);
        builder.Property(build => build.ChampionId).HasMaxLength(64);
        builder.Property(build => build.GameVersion).HasMaxLength(24);
        builder.Property(build => build.Runes).HasColumnType("jsonb");
        builder.Property(build => build.Steps).HasColumnType("jsonb");
        builder.Property(build => build.IsPublic).HasLegacyDefault(false);
        builder.Property(build => build.ShareToken).HasMaxLength(24);
        builder.Property(build => build.GameMode).HasMaxLength(16).HasLegacyDefault("sr");
        builder.Property(build => build.Language).HasMaxLength(8).HasLegacyDefault("en_US");

        builder.HasIndex(build => build.ShareToken)
            .IsUnique()
            .HasDatabaseName("uniq_ab264a5d6594dd6");
        builder.HasIndex(build => build.OwnerId).HasDatabaseName("idx_ab264a57e3c61f9");
        builder.HasOne(build => build.Owner)
            .WithMany()
            .HasForeignKey(build => build.OwnerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ab264a57e3c61f9");
    }
}
