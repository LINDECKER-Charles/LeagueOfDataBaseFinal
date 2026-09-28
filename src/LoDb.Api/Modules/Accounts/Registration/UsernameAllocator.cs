using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoDb.Domain.Text;

namespace LoDb.Api.Modules.Accounts.Registration;

/// <summary>
/// Derives a free username from the hints of a Google profile (given name, local part of the
/// e-mail), as the legacy <c>UsernameAllocator</c> does.
/// </summary>
/// <remarks>
/// The legacy slugger transliterates any script to ASCII; this one only folds accents, so a
/// name written in another script falls through to the next hint, or to "Summoner".
/// </remarks>
internal static class UsernameAllocator
{
    public const string Fallback = "Summoner";

    private const int MaxLength = 24;
    private const int FirstSuffix = 2;

    // Beyond sequential probing, random suffixes guarantee termination.
    private const int MaxSequentialSuffix = 50;
    private const int RandomSuffixMin = 100_000;
    private const int RandomSuffixMax = 999_999;

    private const char Separator = '-';

    /// <summary>
    /// The first usable hint, suffixed with 2 to 50 and then with random digits until
    /// <paramref name="isTaken"/> says no.
    /// </summary>
    public static async Task<string> AllocateAsync(
        IEnumerable<string?> hints,
        Func<string, CancellationToken, Task<bool>> isTaken,
        CancellationToken cancellationToken)
    {
        var name = hints.Select(Normalize).FirstOrDefault(static hint => hint is not null)
            ?? Fallback;
        if (!await isTaken(name, cancellationToken))
        {
            return name;
        }

        for (var suffix = FirstSuffix; suffix <= MaxSequentialSuffix; suffix++)
        {
            var candidate = WithSuffix(name, suffix);
            if (!await isTaken(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return await DrawAsync(name, isTaken, cancellationToken);
    }

    /// <summary>
    /// The hint as a username: accents folded, anything but ASCII letters and digits turned
    /// into dashes, capped at 24; null when too short to be one.
    /// </summary>
    public static string? Normalize(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return null;
        }

        // The slug starts with a letter or a digit, as the legacy trim of "-_." ensured.
        var slug = Slug(TextFolding.RemoveDiacritics(hint.Trim()));
        var name = slug.Length > MaxLength ? slug[..MaxLength] : slug;
        return RegistrationRules.IsUsername(name) ? name : null;
    }

    // Runs of other characters become one dash, none at either end, as the slugger does.
    private static string Slug(string text)
    {
        var slug = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != Separator)
            {
                slug.Append(Separator);
            }
        }

        return slug.ToString().TrimEnd(Separator);
    }

    private static async Task<string> DrawAsync(
        string name,
        Func<string, CancellationToken, Task<bool>> isTaken,
        CancellationToken cancellationToken)
    {
        string drawn;
        do
        {
            drawn = WithSuffix(
                name,
                RandomNumberGenerator.GetInt32(RandomSuffixMin, RandomSuffixMax + 1));
        }
        while (await isTaken(drawn, cancellationToken));
        return drawn;
    }

    // The name is shortened so that the suffixed one still fits the column.
    private static string WithSuffix(string name, int suffix)
    {
        var digits = suffix.ToString(CultureInfo.InvariantCulture);
        var kept = Math.Min(name.Length, MaxLength - digits.Length);
        return name[..kept] + digits;
    }
}
