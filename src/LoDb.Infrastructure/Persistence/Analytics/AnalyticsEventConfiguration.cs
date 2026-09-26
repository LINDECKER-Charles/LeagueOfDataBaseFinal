using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Analytics;

/// <remarks>
/// The migration creates the table in raw SQL, since EF cannot declare a partitioned table:
/// this model has to match it column for column.
/// </remarks>
internal sealed class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.ToTable("analytics_event", static table => table.HasCheckConstraint(
            "ck_analytics_event_origin",
            "origin IN ('page', 'navigation')"));
        // A key of a partitioned table must hold the partition key.
        builder.HasKey(analyticsEvent => new { analyticsEvent.OccurredAt, analyticsEvent.Id });

        builder.Property(analyticsEvent => analyticsEvent.Id)
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();
        builder.Property(analyticsEvent => analyticsEvent.Origin)
            .HasMaxLength(16)
            .HasConversion(AnalyticsColumns.OriginConverter);
        builder.Property(analyticsEvent => analyticsEvent.Route)
            .HasMaxLength(AnalyticsEvent.RouteMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Path)
            .HasMaxLength(AnalyticsEvent.PathMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Type)
            .HasMaxLength(AnalyticsEvent.TypeMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Kind)
            .HasMaxLength(AnalyticsEvent.KindMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Entity)
            .HasMaxLength(AnalyticsEvent.EntityMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Version)
            .HasMaxLength(AnalyticsEvent.VersionMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Lang)
            .HasMaxLength(AnalyticsEvent.LangMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Locale)
            .HasMaxLength(AnalyticsEvent.LocaleMaxLength);
        ConfigureClientColumns(builder);
    }

    private static void ConfigureClientColumns(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.Property(analyticsEvent => analyticsEvent.Ip)
            .HasMaxLength(AnalyticsEvent.IpMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Visitor)
            .HasMaxLength(AnalyticsEvent.VisitorMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.UserAgent)
            .HasMaxLength(AnalyticsEvent.UserAgentMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Browser)
            .HasMaxLength(AnalyticsEvent.BrowserMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Os)
            .HasMaxLength(AnalyticsEvent.OsMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Device)
            .HasMaxLength(AnalyticsEvent.DeviceMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.RefererHost)
            .HasMaxLength(AnalyticsEvent.RefererHostMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.RefererSource)
            .HasMaxLength(AnalyticsEvent.RefererSourceMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.Country)
            .HasMaxLength(AnalyticsEvent.CountryMaxLength);
        builder.Property(analyticsEvent => analyticsEvent.CountryName)
            .HasMaxLength(AnalyticsEvent.CountryNameMaxLength);
    }
}
