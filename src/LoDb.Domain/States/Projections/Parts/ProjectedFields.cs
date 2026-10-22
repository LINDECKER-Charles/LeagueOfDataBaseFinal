using System.Globalization;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Text;

namespace LoDb.Domain.States.Projections.Parts;

/// <summary>
/// The fields of a projection, each as zero or one <see cref="ProjectedField"/>: a field
/// without a value is left out, so a field present on one side only reads as changed.
/// </summary>
internal static class ProjectedFields
{
    private const string True = "true";
    private const string False = "false";
    private const string RoundTrip = "R";
    private const char LineBreak = '\n';

    /// <summary>The fields of every group, in the order given.</summary>
    public static IReadOnlyList<ProjectedField> Of(params IEnumerable<ProjectedField>[] groups) =>
        [.. groups.SelectMany(static group => group)];

    /// <summary>A value shown as is; none when missing or empty.</summary>
    public static IEnumerable<ProjectedField> Value(FieldKey key, string? value) =>
        string.IsNullOrEmpty(value) ? [] : [Field(key, [value])];

    public static IEnumerable<ProjectedField> Number(FieldKey key, double? value) =>
        value is { } number ? [Field(key, [Invariant(number)])] : [];

    public static IEnumerable<ProjectedField> Flag(FieldKey key, bool value) =>
        [Field(key, [value ? True : False])];

    /// <summary>A Data Dragon text in <see cref="DdragonText.PlainText"/> form.</summary>
    public static IEnumerable<ProjectedField> Text(FieldKey key, string? html) =>
        Value(key, DdragonText.PlainText(html));

    /// <summary>Texts shown one per line (tips), as one text.</summary>
    public static IEnumerable<ProjectedField> Lines(FieldKey key, IEnumerable<string> htmls) =>
        Value(key, string.Join(
            LineBreak,
            htmls.Select(DdragonText.PlainText).Where(static line => line.Length > 0)));

    /// <summary>Values whose order shows; none when empty.</summary>
    public static IEnumerable<ProjectedField> List(FieldKey key, IEnumerable<string> values)
    {
        IReadOnlyList<string> list = [.. values];
        return list.Count == 0 ? [] : [Field(key, list)];
    }

    /// <summary>Values without order: sorted ordinal, each once; none when empty.</summary>
    public static IEnumerable<ProjectedField> Set(FieldKey key, IEnumerable<string> values) =>
        List(key, values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    public static string Invariant(double number) =>
        number.ToString(RoundTrip, CultureInfo.InvariantCulture);

    public static string Invariant(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static ProjectedField Field(FieldKey key, IReadOnlyList<string> values) =>
        new() { Key = key, Values = values };
}
