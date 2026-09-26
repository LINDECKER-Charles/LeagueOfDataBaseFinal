using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence;

/// <summary>
/// The legacy <c>timestamp(0) without time zone</c> columns, whose values the legacy stack
/// writes in UTC (<c>date.timezone = UTC</c>).
/// </summary>
/// <remarks>
/// Npgsql refuses a UTC <see cref="DateTime"/> on such a column: these columns, and only
/// these, go through a converter that treats the stored value as UTC. Legacy columns already
/// in <c>timestamptz</c> need none, and every new column is <c>timestamptz</c>. The list goes
/// away with the <c>contract</c> phase, which turns these columns into <c>timestamptz</c>.
/// </remarks>
internal static class LegacyUtcTimestamps
{
    public const string ColumnType = "timestamp(0) without time zone";

    /// <summary>Every column the converter applies to, and no other.</summary>
    public static readonly IReadOnlyList<(string Table, string Column)> Columns =
    [
        ("build_votes", "created_at"),
        ("builds", "created_at"),
        ("builds", "updated_at"),
        ("contact_messages", "created_at"),
        ("contact_messages", "handled_at"),
        ("donations", "created_at"),
        ("users", "created_at"),
    ];

    /// <summary>
    /// UTC wall clock, truncated to the second like Doctrine does, instead of the rounding
    /// PostgreSQL would apply.
    /// </summary>
    public static readonly ValueConverter<DateTimeOffset, DateTime> Converter = new(
        value => DateTime.SpecifyKind(
            value.UtcDateTime.AddTicks(-(value.UtcDateTime.Ticks % TimeSpan.TicksPerSecond)),
            DateTimeKind.Unspecified),
        value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

    public static void Apply(IMutableModel model)
    {
        foreach (var (table, column) in Columns)
        {
            var property = Find(model, table, column);
            property.SetColumnType(ColumnType);
            property.SetValueConverter(Converter);
        }
    }

    private static IMutableProperty Find(IMutableModel model, string table, string column) =>
        model.GetEntityTypes()
            .Where(entity => entity.GetTableName() == table)
            .SelectMany(static entity => entity.GetProperties())
            .SingleOrDefault(property => property.GetColumnName() == column)
        ?? throw new InvalidOperationException($"No mapped column {table}.{column}.");
}
