using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Analytics;

internal sealed class AnalyticsDailyConfiguration : IEntityTypeConfiguration<AnalyticsDaily>
{
    public void Configure(EntityTypeBuilder<AnalyticsDaily> builder)
    {
        builder.ToTable("analytics_daily", static table =>
        {
            table.HasCheckConstraint(
                "ck_analytics_daily_source",
                "source IN ('events', 'import')");
            table.HasCheckConstraint(
                "ck_analytics_daily_shape",
                "jsonb_typeof(totals) = 'object' AND jsonb_typeof(buckets) = 'object'"
                + " AND jsonb_typeof(visitors) = 'array'"
                + " AND jsonb_typeof(country_names) = 'object'");
        });
        builder.HasKey(daily => daily.Day);

        builder.Property(daily => daily.Source)
            .HasMaxLength(16)
            .HasConversion(AnalyticsColumns.SourceConverter);
        builder.Property(daily => daily.Totals).HasColumnType("jsonb");
        builder.Property(daily => daily.Buckets).HasColumnType("jsonb");
        builder.Property(daily => daily.Visitors).HasColumnType("jsonb");
        builder.Property(daily => daily.CountryNames).HasColumnType("jsonb");
    }
}
