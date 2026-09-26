using System.Globalization;
using LoDb.Ingestion.Ddragon.Raw;

namespace LoDb.Ingestion.Normalization.Mapping;

/// <summary>
/// Reads the optional values of the raw files into the domain's non-null shapes: a missing
/// text is empty, a missing list has no entry, a null entry is dropped.
/// </summary>
internal static class RawValues
{
    public static string Text(string? value) => value ?? string.Empty;

    /// <summary>An optional reference ("requiredChampion"): empty means none.</summary>
    public static string? Reference(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>The file name of an <c>image</c> node, the only part the site uses.</summary>
    public static string ImageFile(RawImage? image) => Text(image?.Full);

    /// <summary>The non-null strings, in order (0.x items list null recipe ids, UP 3).</summary>
    public static IReadOnlyList<string> Strings(List<string?>? values) =>
        values is null ? [] : [.. values.OfType<string>()];

    /// <summary>The numeric entries of a stats map.</summary>
    public static IReadOnlyDictionary<string, double> Numbers(Dictionary<string, double?>? values)
    {
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, value) in values ?? [])
        {
            if (value is { } number)
            {
                numbers.Add(key, number);
            }
        }

        return numbers;
    }

    /// <summary>The flags of a map keyed by numeric ids ("11"); other keys are dropped.</summary>
    public static IReadOnlyDictionary<int, bool> Flags(Dictionary<string, bool?>? values)
    {
        var flags = new Dictionary<int, bool>();
        foreach (var (key, value) in values ?? [])
        {
            if (value is { } flag
                && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                flags.TryAdd(id, flag);
            }
        }

        return flags;
    }
}
