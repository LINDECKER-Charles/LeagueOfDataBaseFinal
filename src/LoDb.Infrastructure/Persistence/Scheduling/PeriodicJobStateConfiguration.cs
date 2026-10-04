using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Scheduling;

internal sealed class PeriodicJobStateConfiguration : IEntityTypeConfiguration<PeriodicJobState>
{
    public void Configure(EntityTypeBuilder<PeriodicJobState> builder)
    {
        builder.ToTable("periodic_job");
        builder.HasKey(job => job.Name);

        builder.Property(job => job.Name).HasMaxLength(64);
    }
}
