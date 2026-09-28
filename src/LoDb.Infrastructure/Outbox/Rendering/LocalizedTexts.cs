using System.Globalization;
using LoDb.Domain.Languages;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The texts of one locale, each key falling back to English: see
/// <see cref="EmailTexts.For"/>.
/// </summary>
internal sealed class LocalizedTexts(
    UiLocale locale,
    IReadOnlyDictionary<string, string>? own,
    IReadOnlyDictionary<string, string> fallback)
{
    private const int MinutesPerHour = 60;

    /// <summary>Locale of the catalog used, English when the asked one has none.</summary>
    public UiLocale Locale { get; } = locale;

    /// <summary>Value of the <c>lang</c> attribute of the HTML part.</summary>
    public string Language => UiLocales.Code(Locale);

    public string Get(string key) =>
        TryGet(key) ?? throw new InvalidOperationException($"No e-mail text {key}.");

    /// <summary>The text with each <c>%placeholder%</c> replaced by its value.</summary>
    public string Get(string key, string placeholder, string value) =>
        Get(key).Replace($"%{placeholder}%", value, StringComparison.Ordinal);

    public string? TryGet(string key) =>
        own is not null && own.TryGetValue(key, out var text)
            ? text
            : fallback.GetValueOrDefault(key);

    /// <summary>
    /// A link lifetime, in hours when it is a whole number of them, as the legacy bundles
    /// wrote it ("1 hour", "30 minutes").
    /// </summary>
    public string Duration(int minutes)
    {
        var (unit, count) = minutes % MinutesPerHour == 0
            ? ("hour", minutes / MinutesPerHour)
            : ("minute", minutes);
        var form = count == 1 ? "one" : "other";
        return Get(
            $"email.duration.{unit}.{form}",
            "count",
            count.ToString(CultureInfo.InvariantCulture));
    }
}
