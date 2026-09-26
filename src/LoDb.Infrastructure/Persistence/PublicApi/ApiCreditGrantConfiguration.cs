using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.PublicApi;

internal sealed class ApiCreditGrantConfiguration : IEntityTypeConfiguration<ApiCreditGrant>
{
    public void Configure(EntityTypeBuilder<ApiCreditGrant> builder)
    {
        builder.ToTable("api_credit_grants", static table =>
        {
            table.HasCheckConstraint(
                "ck_api_credit_grants_source",
                "source IN ('purchase', 'migration', 'reconciliation', 'admin')");
            table.HasCheckConstraint(
                "ck_api_credit_grants_session",
                "(source = 'purchase') = (stripe_session_id IS NOT NULL)");
            table.HasCheckConstraint("ck_api_credit_grants_requests", "requests > 0");
            table.HasCheckConstraint(
                "ck_api_credit_grants_expires_at",
                "expires_at > purchased_at");
            table.HasCheckConstraint(
                "ck_api_credit_grants_expired",
                "(expired_at IS NULL AND expired_requests IS NULL)"
                + " OR (expired_at IS NOT NULL"
                + " AND expired_requests BETWEEN 0 AND requests)");
        });
        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.Source)
            .HasMaxLength(16)
            .HasConversion(PublicApiColumns.SourceConverter);
        builder.Property(grant => grant.StripeSessionId).HasMaxLength(255);

        builder.HasIndex(grant => grant.StripeSessionId).IsUnique();
        builder.HasIndex(grant => new { grant.ApiKeyId, grant.PurchasedAt });
        // The expiry job's query: the live grants that are due.
        builder.HasIndex(grant => grant.ExpiresAt)
            .HasDatabaseName("ix_api_credit_grants_due")
            .HasFilter("expired_at IS NULL");
        builder.HasOne(grant => grant.ApiKey)
            .WithMany()
            .HasForeignKey(grant => grant.ApiKeyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
