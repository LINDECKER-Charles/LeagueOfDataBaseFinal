namespace LoDb.Domain.Builds.Metadata;

/// <summary>
/// The length of a text as a person counts it and as PostgreSQL bounds a varchar: in
/// characters, an emoji counting once, not in UTF-16 code units.
/// </summary>
internal static class TextLength
{
    public static int Of(string text) => text.EnumerateRunes().Count();

    public static bool IsWithin(string text, int max) => Of(text) <= max;

    public static bool IsBetween(string text, int min, int max)
    {
        var length = Of(text);
        return length >= min && length <= max;
    }
}
