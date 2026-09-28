using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Billing;

internal sealed class StripeEventConfiguration : IEntityTypeConfiguration<StripeEvent>
{
    private static readonly ValueConverter<StripeEventStatus, string> StatusConverter = new(
        status => ToText(status),
        value => ParseStatus(value));

    public void Configure(EntityTypeBuilder<StripeEvent> builder)
    {
        builder.ToTable("stripe_event", static table => table.HasCheckConstraint(
            "ck_stripe_event_status",
            "status IN ('processed', 'ignored')"));
        builder.HasKey(stripeEvent => stripeEvent.Id);

        builder.Property(stripeEvent => stripeEvent.Id).HasMaxLength(255);
        builder.Property(stripeEvent => stripeEvent.Type).HasMaxLength(128);
        builder.Property(stripeEvent => stripeEvent.Status)
            .HasMaxLength(16)
            .HasConversion(StatusConverter);
    }

    private static string ToText(StripeEventStatus status) => status switch
    {
        StripeEventStatus.Processed => "processed",
        StripeEventStatus.Ignored => "ignored",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static StripeEventStatus ParseStatus(string value) => value switch
    {
        "processed" => StripeEventStatus.Processed,
        "ignored" => StripeEventStatus.Ignored,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
