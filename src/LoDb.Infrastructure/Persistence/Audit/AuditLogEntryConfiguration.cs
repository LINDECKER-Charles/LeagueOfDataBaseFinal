using LoDb.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Audit;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    private static readonly ValueConverter<AuditActorType, string> ActorTypeConverter = new(
        type => AuditVocabulary.ToText(type),
        value => AuditVocabulary.ParseActorType(value));

    private static readonly ValueConverter<AuditAction, string> ActionConverter = new(
        action => AuditVocabulary.ToText(action),
        value => AuditVocabulary.ParseAction(value));

    private static readonly ValueConverter<AuditOutcome, string> OutcomeConverter = new(
        outcome => AuditVocabulary.ToText(outcome),
        value => AuditVocabulary.ParseOutcome(value));

    private static readonly ValueConverter<AuditTargetType, string> TargetTypeConverter = new(
        type => AuditVocabulary.ToText(type),
        value => AuditVocabulary.ParseTargetType(value));

    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        // Actions and target types grow with the lots, which write no migration: only the
        // two closed sets are checked here.
        builder.ToTable("audit_log", static table =>
        {
            table.HasCheckConstraint(
                "ck_audit_log_actor_type",
                "actor_type IN ('user', 'admin', 'anonymous')");
            table.HasCheckConstraint(
                "ck_audit_log_outcome",
                "outcome IN ('success', 'failure', 'denied')");
        });
        builder.HasKey(entry => entry.Id);

        ConfigureColumns(builder);

        // The journal is read newest first, whole or for one actor or one action.
        builder.HasIndex(entry => entry.OccurredAt).HasDatabaseName("ix_audit_log_occurred_at");
        builder.HasIndex(entry => new { entry.ActorId, entry.OccurredAt })
            .HasDatabaseName("ix_audit_log_actor");
        builder.HasIndex(entry => new { entry.Action, entry.OccurredAt })
            .HasDatabaseName("ix_audit_log_action");
    }

    private static void ConfigureColumns(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.Property(entry => entry.ActorType)
            .HasMaxLength(16)
            .HasConversion(ActorTypeConverter);
        builder.Property(entry => entry.Actor).HasMaxLength(AuditLogEntry.ActorMaxLength);
        builder.Property(entry => entry.Action).HasMaxLength(64).HasConversion(ActionConverter);
        builder.Property(entry => entry.Outcome).HasMaxLength(16).HasConversion(OutcomeConverter);
        builder.Property(entry => entry.TargetType)
            .HasMaxLength(32)
            .HasConversion(TargetTypeConverter);
        builder.Property(entry => entry.TargetId).HasMaxLength(AuditLogEntry.TargetIdMaxLength);
        builder.Property(entry => entry.Target).HasMaxLength(AuditLogEntry.TargetMaxLength);
        builder.Property(entry => entry.Ip).HasMaxLength(AuditLogEntry.IpMaxLength);
        builder.Property(entry => entry.Route).HasMaxLength(AuditLogEntry.RouteMaxLength);
        builder.Property(entry => entry.Meta).HasColumnType("jsonb");
    }
}
