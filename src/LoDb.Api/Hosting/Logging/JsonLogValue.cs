using System.Globalization;
using System.Text.Json;

namespace LoDb.Api.Hosting.Logging;

/// <summary>
/// Writes a template property of a log record as a JSON value.
/// </summary>
/// <remarks>
/// Numbers and booleans keep their JSON type so they can be compared once parsed; anything
/// else is written as its invariant text, never serialized as an object graph.
/// </remarks>
internal static class JsonLogValue
{
    public static void Write(Utf8JsonWriter writer, string key, object? value)
    {
        if (TryWriteNumber(writer, key, value))
        {
            return;
        }

        switch (value)
        {
            case null:
                writer.WriteNull(key);
                break;
            case bool flag:
                writer.WriteBoolean(key, flag);
                break;
            case DateTimeOffset moment:
                writer.WriteString(key, moment);
                break;
            case DateTime moment:
                writer.WriteString(key, moment);
                break;
            default:
                writer.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static bool TryWriteNumber(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case sbyte or byte or short or ushort or int or uint or long:
                writer.WriteNumber(key, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return true;
            case ulong unsigned:
                writer.WriteNumber(key, unsigned);
                return true;
            case double real when double.IsFinite(real):
                writer.WriteNumber(key, real);
                return true;
            case float single when float.IsFinite(single):
                writer.WriteNumber(key, single);
                return true;
            case decimal exact:
                writer.WriteNumber(key, exact);
                return true;
            default:
                return false;
        }
    }
}
