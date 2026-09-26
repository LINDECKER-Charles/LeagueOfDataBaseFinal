using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Ddragon;

internal sealed class DdragonAssetConfiguration : IEntityTypeConfiguration<DdragonAsset>
{
    public void Configure(EntityTypeBuilder<DdragonAsset> builder)
    {
        builder.ToTable("ddragon_asset", static table =>
        {
            table.HasCheckConstraint("ck_ddragon_asset_status", "status IN ('present', 'absent')");
            table.HasCheckConstraint(
                "ck_ddragon_asset_blob",
                "(status = 'present' AND sha256 IS NOT NULL AND extension IS NOT NULL)"
                + " OR (status = 'absent' AND sha256 IS NULL AND extension IS NULL)");
            table.HasCheckConstraint("ck_ddragon_asset_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_ddragon_asset_extension", "extension ~ '^[a-z0-9]{1,8}$'");
        });
        builder.HasKey(asset => new { asset.Version, asset.Type, asset.Key });

        builder.Property(asset => asset.Version).HasMaxLength(32);
        builder.Property(asset => asset.Type).HasMaxLength(32);
        builder.Property(asset => asset.Key).HasMaxLength(255);
        builder.Property(asset => asset.Status)
            .HasMaxLength(16)
            .HasConversion(DdragonColumns.AssetStatusConverter);
        builder.Property(asset => asset.Sha256).HasMaxLength(64);
        builder.Property(asset => asset.Extension).HasMaxLength(8);
    }
}
