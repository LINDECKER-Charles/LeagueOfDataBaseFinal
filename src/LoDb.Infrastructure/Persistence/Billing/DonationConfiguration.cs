using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Billing;

internal sealed class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.ToTable("donations");
        builder.HasKey(donation => donation.Id).HasName("donations_pkey");

        builder.Property(donation => donation.StripeSessionId).HasMaxLength(255);
        builder.Property(donation => donation.Currency).HasMaxLength(3);

        builder.HasIndex(donation => donation.StripeSessionId)
            .IsUnique()
            .HasDatabaseName("uniq_cde989621a314a57");
        builder.HasIndex(donation => donation.UserId).HasDatabaseName("idx_cde98962a76ed395");
        builder.HasOne(donation => donation.User)
            .WithMany()
            .HasForeignKey(donation => donation.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_cde98962a76ed395");
    }
}
