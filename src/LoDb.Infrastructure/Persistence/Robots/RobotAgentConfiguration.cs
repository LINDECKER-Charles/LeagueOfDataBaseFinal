using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Robots;

/// <remarks>One row per distinct text: the writer upserts it, the hits point to its id.</remarks>
internal sealed class RobotAgentConfiguration : IEntityTypeConfiguration<RobotAgent>
{
    public void Configure(EntityTypeBuilder<RobotAgent> builder)
    {
        builder.ToTable("robot_agent");
        builder.HasKey(agent => agent.Id);
        builder.Property(agent => agent.Id).UseIdentityByDefaultColumn();
        builder.Property(agent => agent.Agent).HasMaxLength(RobotAgent.AgentMaxLength);
        builder.HasIndex(agent => agent.Agent)
            .IsUnique()
            .HasDatabaseName("ux_robot_agent_agent");
    }
}
