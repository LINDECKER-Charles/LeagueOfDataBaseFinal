using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.PublicApi;

internal sealed class ApiUsageConfiguration : IEntityTypeConfiguration<ApiUsage>
{
    public void Configure(EntityTypeBuilder<ApiUsage> builder)
    {
        builder.ToTable("api_usage");
        builder.HasKey(usage => usage.Id).HasName("api_usage_pkey");

        builder.Property(usage => usage.Requests).HasLegacyDefault(0L);

        builder.HasIndex(usage => new { usage.ApiKeyId, usage.Day })
            .IsUnique()
            .HasDatabaseName("uniq_api_usage_key_day");
        builder.HasIndex(usage => usage.ApiKeyId).HasDatabaseName("idx_f47e0aa08be312b3");
        builder.HasOne(usage => usage.ApiKey)
            .WithMany()
            .HasForeignKey(usage => usage.ApiKeyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_f47e0aa08be312b3");
    }
}
