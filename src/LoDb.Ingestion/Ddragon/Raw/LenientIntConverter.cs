using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoDb.Ingestion.Ddragon.Raw;

/// <summary>
/// Reads an integer written as a number or as a string ("-1"), and anything else as missing.
/// </summary>
/// <remarks>
/// Data Dragon writes <c>maxammo</c> as a string: one odd value must not fail a whole dataset.
/// </remarks>
internal sealed class LenientIntConverter : JsonConverter<int?>
{
    public override int? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var number) ? number : null;
            case JsonTokenType.String:
                return int.TryParse(
                    reader.GetString(),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is { } number)
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
