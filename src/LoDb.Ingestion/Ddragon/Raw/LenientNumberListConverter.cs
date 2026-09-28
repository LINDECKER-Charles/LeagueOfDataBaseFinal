using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoDb.Ingestion.Ddragon.Raw;

/// <summary>
/// Reads a per-rank number list, and any other value as an empty list.
/// </summary>
/// <remarks>
/// Before 4.x a summoner spell's <c>range</c> may be the string "self" instead of a list; its
/// <c>rangeBurn</c> keeps that word for the page. Numbers written as strings are read too.
/// </remarks>
internal sealed class LenientNumberListConverter : JsonConverter<List<double>>
{
    public override List<double> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        var numbers = new List<double>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (ReadNumber(ref reader) is { } number)
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<double> value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartArray();
        foreach (var number in value)
        {
            writer.WriteNumberValue(number);
        }

        writer.WriteEndArray();
    }

    private static double? ReadNumber(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                return double.TryParse(
                    reader.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null;
            default:
                reader.Skip();
                return null;
        }
    }
}
