using System.Buffers;
using LoDb.Domain.Text;

namespace LoDb.Domain.Paths;

/// <summary>
/// The slug of an entity path: readable ASCII derived from the en_US name.
/// </summary>
/// <remarks>
/// The slug is decorative, the id identifies: it only has to be stable and readable in every
/// locale. Markup is reduced, accents folded and apostrophes dropped ("Rabadon's" gives
/// "rabadons", not "rabadon-s"); every other run of non-alphanumeric characters becomes one
/// hyphen. Letters without an ASCII base (a non-Latin name) leave nothing behind.
/// </remarks>
internal static class Slugs
{
    // Straight, left and right quotation marks, modifier letter apostrophe.
    private static readonly SearchValues<char> Apostrophes =
        SearchValues.Create("'‘’ʼ");

    internal static string From(string? name) =>
        AsciiCase.Hyphenate(
            TextFolding.RemoveDiacritics(DdragonText.PlainName(name)),
            Apostrophes);
}
