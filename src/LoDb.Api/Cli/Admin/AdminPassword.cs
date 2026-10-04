using System.Security.Cryptography;

namespace LoDb.Api.Cli.Admin;

/// <summary>
/// A random password for an administrator account created from the shell, meeting the CNIL
/// policy: every class of character, well beyond its minimum length.
/// </summary>
internal static class AdminPassword
{
    private const int Length = 24;
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Specials = "-_.!?@#%+=";

    // Ambiguous characters (l, I, O, 0, 1) are left out: the password is read off a terminal.
    private static readonly string[] Classes = [Lowercase, Uppercase, Digits, Specials];

    public static string Generate()
    {
        var all = string.Concat(Classes);
        var characters = new char[Length];
        for (var index = 0; index < Length; index++)
        {
            // One of each class first, so that no draw can miss one.
            var pool = index < Classes.Length ? Classes[index] : all;
            characters[index] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }

        RandomNumberGenerator.Shuffle(characters.AsSpan());
        return new string(characters);
    }
}
