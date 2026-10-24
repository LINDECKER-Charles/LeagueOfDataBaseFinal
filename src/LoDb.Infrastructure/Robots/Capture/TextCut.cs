namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>A text cut to the length of its column, never between the halves of a pair.</summary>
internal static class TextCut
{
    public static string To(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length <= maxLength)
        {
            return value;
        }

        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }
}
