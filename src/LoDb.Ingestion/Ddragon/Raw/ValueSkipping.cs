using System.Text.Json;

namespace LoDb.Ingestion.Ddragon.Raw;

/// <summary>Passes over the value a lenient converter does not read.</summary>
internal static class ValueSkipping
{
    /// <summary>Moves <paramref name="reader"/> to the end of its current value.</summary>
    /// <remarks>
    /// A file read as a stream reaches a converter in blocks, and <c>Skip</c> throws on any
    /// block but the last, whatever the value: a file over the serializer's buffer (16 KB)
    /// then failed whole. The serializer has read the converter's value ahead, so
    /// <c>TrySkip</c> always has it.
    /// </remarks>
    public static void Skip(ref Utf8JsonReader reader)
    {
        if (!reader.TrySkip())
        {
            throw new JsonException("The value to skip is not buffered whole.");
        }
    }
}
