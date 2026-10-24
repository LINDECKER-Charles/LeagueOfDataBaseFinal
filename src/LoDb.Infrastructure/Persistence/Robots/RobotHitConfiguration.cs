using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Robots;

/// <remarks>
/// Read newest first, over the whole journal or for the agents of one robot; the retention
/// deletes by age.
/// </remarks>
internal sealed class RobotHitConfiguration : IEntityTypeConfiguration<RobotHit>
{
    public void Configure(EntityTypeBuilder<RobotHit> builder)
    {
        builder.ToTable("robot_hit", static table =>
        {
            table.HasCheckConstraint("ck_robot_hit_detection", "detection IN ('agent', 'rate')");
            table.HasCheckConstraint("ck_robot_hit_duration", "duration_ms >= 0");
        });
        builder.HasKey(hit => hit.Id);
        builder.Property(hit => hit.Id).UseIdentityByDefaultColumn();
        builder.Property(hit => hit.Detection).HasMaxLength(RobotHit.LabelMaxLength);
        builder.Property(hit => hit.Verdict).HasMaxLength(RobotHit.LabelMaxLength);
        builder.Property(hit => hit.Network).HasMaxLength(RobotHit.NetworkMaxLength);
        builder.Property(hit => hit.Method).HasMaxLength(RobotHit.MethodMaxLength);
        builder.Property(hit => hit.Target).HasMaxLength(RobotHit.TargetMaxLength);
        builder.Property(hit => hit.Section).HasMaxLength(RobotHit.LabelMaxLength);
        builder.Property(hit => hit.Cache).HasMaxLength(RobotHit.LabelMaxLength);
        builder.HasOne<RobotAgent>()
            .WithMany()
            .HasForeignKey(hit => hit.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(hit => hit.OccurredAt).HasDatabaseName("ix_robot_hit_occurred_at");
        builder.HasIndex(hit => new { hit.AgentId, hit.OccurredAt })
            .HasDatabaseName("ix_robot_hit_agent_occurred_at");
    }
}
