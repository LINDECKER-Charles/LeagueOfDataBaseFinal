using System.Globalization;

namespace LoDb.Api.Modules.Profiles.Owner;

/// <summary>
/// The e-mail as the profile shows it: enough to recognize it, nothing for someone looking
/// over the shoulder to harvest (<c>charles@outlook.fr</c> reads <c>c***@outlook.fr</c>).
/// </summary>
internal static class EmailMask
{
    private const string Mask = "***";
    private const char At = '@';

    public static string Apply(string? email)
    {
        var at = email?.IndexOf(At, StringComparison.Ordinal) ?? -1;
        if (email is null || at < 1)
        {
            return Mask;
        }

        // A whole text element, so that a character outside the BMP is not cut in half.
        var first = Math.Min(StringInfo.GetNextTextElementLength(email), at);
        return email[..first] + Mask + email[at..];
    }
}
