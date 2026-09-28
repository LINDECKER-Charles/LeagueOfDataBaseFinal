using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Outbox;

internal sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        // No check on the template: a lot that adds one would need a migration of its own,
        // and only the schema chantiers write migrations. The enum keeps the set closed.
        builder.ToTable("email_outbox", static table =>
        {
            table.HasCheckConstraint(
                "ck_email_outbox_status",
                "status IN ('pending', 'sent', 'dead')");
            table.HasCheckConstraint("ck_email_outbox_attempts", "attempts >= 0");
        });
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Recipient).HasMaxLength(180);
        builder.Property(message => message.Template)
            .HasMaxLength(32)
            .HasConversion(OutboxColumns.TemplateConverter);
        builder.Property(message => message.Locale)
            .HasMaxLength(8)
            .HasConversion(OutboxColumns.LocaleConverter);
        builder.Property(message => message.Model).HasColumnType("jsonb");
        builder.Property(message => message.Status)
            .HasMaxLength(16)
            .HasConversion(OutboxColumns.StatusConverter);
        builder.Property(message => message.Attempts).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(message => message.LastErrorCode).HasMaxLength(64);

        // The worker's query: the pending messages due first, sent ones left out.
        builder.HasIndex(message => message.NextAttemptAt)
            .HasDatabaseName("ix_email_outbox_pending")
            .HasFilter("status = 'pending'");
    }
}
