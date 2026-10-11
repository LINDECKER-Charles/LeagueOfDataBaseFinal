using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Billing.Payments;

internal sealed class StripeCheckoutConfiguration : IEntityTypeConfiguration<StripeCheckout>
{
    public void Configure(EntityTypeBuilder<StripeCheckout> builder)
    {
        builder.ToTable("stripe_checkout", static table =>
        {
            table.HasCheckConstraint(
                "ck_stripe_checkout_kind",
                "kind IN ('api_pack', 'api_plan', 'donation')");
            table.HasCheckConstraint(
                "ck_stripe_checkout_status",
                "status IN ('pending', 'paid', 'failed', 'refused', 'revoked')");
        });
        builder.HasKey(checkout => checkout.Id);

        builder.Property(checkout => checkout.Id).HasMaxLength(255);
        builder.Property(checkout => checkout.Kind)
            .HasMaxLength(16)
            .HasConversion(PaymentColumns.KindConverter);
        builder.Property(checkout => checkout.Status)
            .HasMaxLength(16)
            .HasConversion(PaymentColumns.CheckoutConverter);
        builder.Property(checkout => checkout.PaymentIntentId).HasMaxLength(255);
        builder.Property(checkout => checkout.Currency).HasMaxLength(3);

        // A refund or a dispute names the payment, not the session.
        builder.HasIndex(checkout => checkout.PaymentIntentId)
            .IsUnique()
            .HasFilter("payment_intent_id IS NOT NULL");
    }
}
