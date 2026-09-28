using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.PublicApi;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(key => key.Id).HasName("api_keys_pkey");

        builder.Property(key => key.Name).HasMaxLength(64);
        builder.Property(key => key.KeyHash).HasMaxLength(64);
        builder.Property(key => key.KeyPrefix).HasMaxLength(12);
        builder.Property(key => key.Plan).HasMaxLength(16).HasLegacyDefault("free");
        builder.Property(key => key.MonthlyQuota).HasLegacyDefault(500);
        builder.Property(key => key.CreditsBalance).HasLegacyDefault(0L);
        builder.Property(key => key.RateLimitPerMin).HasLegacyDefault(10);
        builder.Property(key => key.IsActive).HasLegacyDefault(true);
        builder.Property(key => key.CreatedAt).HasPrecision(0);
        builder.Property(key => key.RevokedAt).HasPrecision(0).HasNullDefault();
        builder.Property(key => key.StripeCustomerId).HasMaxLength(64).HasNullDefault();
        builder.Property(key => key.StripeSubscriptionId).HasMaxLength(64).HasNullDefault();

        builder.HasIndex(key => key.KeyHash).IsUnique().HasDatabaseName("uniq_9579321f57bfb971");
        builder.HasIndex(key => key.UserId).HasDatabaseName("idx_9579321fa76ed395");
        builder.HasOne(key => key.User)
            .WithMany()
            .HasForeignKey(key => key.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_9579321fa76ed395");
    }
}
