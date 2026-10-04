namespace LoDb.Api.Modules.PublicApi.Trends;

/// <summary>
/// A window of <c>/v1/trends</c>: the last <see cref="Days"/> UTC days, today included.
/// </summary>
internal sealed class TrendRange
{
    private TrendRange(string label, int days)
    {
        Label = label;
        Days = days;
    }

    /// <summary>The default window, when <c>?range=</c> is missing or empty.</summary>
    public static TrendRange Week { get; } = new("7d", 7);

    public static TrendRange Month { get; } = new("30d", 30);

    public static IReadOnlyList<TrendRange> All { get; } = [Week, Month];

    /// <summary>The refusal of a label no window has, which lists them all.</summary>
    public static string UnknownMessage { get; } =
        "range must be " + string.Join(" or ", All.Select(static range => range.Label));

    public string Label { get; }

    public int Days { get; }

    /// <summary>The window of a label, spelled exactly; null when none is.</summary>
    public static TrendRange? Find(string label) =>
        All.FirstOrDefault(range => string.Equals(range.Label, label, StringComparison.Ordinal));

    /// <summary>The first day of the window that ends on <paramref name="today"/>.</summary>
    public DateOnly FirstDay(DateOnly today) => today.AddDays(1 - Days);
}
