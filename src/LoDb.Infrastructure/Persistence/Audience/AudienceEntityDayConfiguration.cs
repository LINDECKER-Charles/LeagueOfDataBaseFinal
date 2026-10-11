using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Audience;

/// <remarks>
/// One row per day and entity. The trends read a window of days, through the key, and the
/// recent days of each entity they bound, through the index by entity.
/// </remarks>
internal sealed class AudienceEntityDayConfiguration : IEntityTypeConfiguration<AudienceEntityDay>
{
    public void Configure(EntityTypeBuilder<AudienceEntityDay> builder)
    {
        builder.ToTable("audience_entity_daily", static table =>
        {
            table.HasCheckConstraint("ck_audience_entity_daily_views", "views >= 0");
            table.HasCheckConstraint("ck_audience_entity_daily_visitors", "visitors >= 0");
        });
        builder.HasKey(day => new { day.Day, day.Entity });
        builder.Property(day => day.Entity).HasMaxLength(AudienceEntityDay.EntityMaxLength);
        builder.HasIndex(day => new { day.Entity, day.Day })
            .HasDatabaseName("ix_audience_entity_daily_entity_day");
    }
}
