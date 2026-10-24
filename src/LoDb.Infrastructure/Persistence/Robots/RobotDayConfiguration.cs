using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Robots;

/// <remarks>The key is every dimension: a batch adds its counts to the row of each.</remarks>
internal sealed class RobotDayConfiguration : IEntityTypeConfiguration<RobotDay>
{
    public void Configure(EntityTypeBuilder<RobotDay> builder)
    {
        builder.ToTable("robot_daily", static table =>
        {
            table.HasCheckConstraint(
                "ck_robot_daily_detection",
                "detection IN ('agent', 'rate')");
            table.HasCheckConstraint(
                "ck_robot_daily_counts",
                "hits >= 0 AND bytes >= 0 AND duration_ms >= 0 AND slow_hits >= 0");
        });
        builder.HasKey(day => new
        {
            day.Day,
            day.AgentId,
            day.Detection,
            day.Verdict,
            day.Network,
            day.Section,
            day.Status,
        });
        builder.Property(day => day.Detection).HasMaxLength(RobotHit.LabelMaxLength);
        builder.Property(day => day.Verdict).HasMaxLength(RobotHit.LabelMaxLength);
        builder.Property(day => day.Network).HasMaxLength(RobotHit.NetworkMaxLength);
        builder.Property(day => day.Section).HasMaxLength(RobotHit.LabelMaxLength);
        builder.HasOne<RobotAgent>()
            .WithMany()
            .HasForeignKey(day => day.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(day => new { day.AgentId, day.Day })
            .HasDatabaseName("ix_robot_daily_agent_day");
    }
}
