using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Persistence.Analytics;

/// <summary>Writes a batch of views to <c>analytics_event</c> in one binary copy.</summary>
/// <remarks>
/// <c>ip</c> and <c>user_agent</c> are left out, so they stay null: nothing reads them, and
/// the columns remain until the schema's contract step drops them.
/// </remarks>
internal sealed class AnalyticsEventWriter(NpgsqlDataSource dataSource) : IAnalyticsEventWriter
{
    // The three typed columns first, then the text ones in the order of TextColumns.
    private const string Copy = """
        COPY analytics_event (occurred_at, status, is_bot, origin, route, path, type, kind,
            entity, version, lang, locale, visitor, browser, os, device,
            referer_host, referer_source, country, country_name)
        FROM STDIN (FORMAT BINARY)
        """;

    private static readonly (Func<AnalyticsEvent, string?> Value, int MaxLength)[] TextColumns =
    [
        (static view => AnalyticsColumns.ToText(view.Origin), int.MaxValue),
        (static view => view.Route, AnalyticsEvent.RouteMaxLength),
        (static view => view.Path, AnalyticsEvent.PathMaxLength),
        (static view => view.Type, AnalyticsEvent.TypeMaxLength),
        (static view => view.Kind, AnalyticsEvent.KindMaxLength),
        (static view => view.Entity, AnalyticsEvent.EntityMaxLength),
        (static view => view.Version, AnalyticsEvent.VersionMaxLength),
        (static view => view.Lang, AnalyticsEvent.LangMaxLength),
        (static view => view.Locale, AnalyticsEvent.LocaleMaxLength),
        (static view => view.Visitor, AnalyticsEvent.VisitorMaxLength),
        (static view => view.Browser, AnalyticsEvent.BrowserMaxLength),
        (static view => view.Os, AnalyticsEvent.OsMaxLength),
        (static view => view.Device, AnalyticsEvent.DeviceMaxLength),
        (static view => view.RefererHost, AnalyticsEvent.RefererHostMaxLength),
        (static view => view.RefererSource, AnalyticsEvent.RefererSourceMaxLength),
        (static view => view.Country, AnalyticsEvent.CountryMaxLength),
        (static view => view.CountryName, AnalyticsEvent.CountryNameMaxLength),
    ];

    public async Task WriteAsync(
        IReadOnlyCollection<AnalyticsEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var importer = await connection.BeginBinaryImportAsync(Copy, cancellationToken);
        foreach (var view in events)
        {
            await WriteRowAsync(importer, view, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
    }

    private static async Task WriteRowAsync(
        NpgsqlBinaryImporter importer,
        AnalyticsEvent view,
        CancellationToken cancellationToken)
    {
        await importer.StartRowAsync(cancellationToken);
        await importer.WriteAsync(
            view.OccurredAt.ToUniversalTime(),
            NpgsqlDbType.TimestampTz,
            cancellationToken);
        await importer.WriteAsync(view.Status, NpgsqlDbType.Smallint, cancellationToken);
        await importer.WriteAsync(view.IsBot, NpgsqlDbType.Boolean, cancellationToken);
        foreach (var (value, maxLength) in TextColumns)
        {
            var text = Cut(value(view), maxLength);
            await (text is null
                ? importer.WriteNullAsync(cancellationToken)
                : importer.WriteAsync(text, NpgsqlDbType.Varchar, cancellationToken));
        }
    }

    // Never between the two halves of a surrogate pair, which would not encode.
    private static string? Cut(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }
}
