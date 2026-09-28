using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Contact;

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("contact_messages");
        builder.HasKey(message => message.Id).HasName("contact_messages_pkey");

        builder.Property(message => message.Category).HasMaxLength(255);
        builder.Property(message => message.Name).HasMaxLength(120).HasNullDefault();
        builder.Property(message => message.Email).HasMaxLength(255);
        builder.Property(message => message.Subject).HasMaxLength(160).HasNullDefault();
        builder.Property(message => message.Locale).HasMaxLength(16).HasNullDefault();
        builder.Property(message => message.Ip).HasMaxLength(64).HasNullDefault();
        builder.Property(message => message.Status).HasMaxLength(255).HasLegacyDefault("new");
        builder.Property(message => message.HandledAt).HasNullDefault();

        builder.HasIndex(message => message.Status).HasDatabaseName("idx_412782017b00651c");
        builder.HasIndex(message => message.CreatedAt).HasDatabaseName("idx_412782018b8e8428");
        builder.HasIndex(message => message.UserId).HasDatabaseName("idx_41278201a76ed395");
        builder.HasOne(message => message.User)
            .WithMany()
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_41278201a76ed395");
    }
}
