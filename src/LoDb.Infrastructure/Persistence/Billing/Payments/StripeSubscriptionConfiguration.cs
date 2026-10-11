using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Billing.Payments;

internal sealed class StripeSubscriptionConfiguration
    : IEntityTypeConfiguration<StripeSubscription>
{
    public void Configure(EntityTypeBuilder<StripeSubscription> builder)
    {
        builder.ToTable("stripe_subscription", static table => table.HasCheckConstraint(
            "ck_stripe_subscription_status",
            "status IN ('entitled', 'suspended', 'ended')"));
        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Id).HasMaxLength(255);
        // As long as api_keys.plan.
        builder.Property(subscription => subscription.Plan).HasMaxLength(16);
        builder.Property(subscription => subscription.Status)
            .HasMaxLength(16)
            .HasConversion(PaymentColumns.SubscriptionConverter);
    }
}
