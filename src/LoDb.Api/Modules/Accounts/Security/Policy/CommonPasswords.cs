using System.Collections.Frozen;

namespace LoDb.Api.Modules.Accounts.Security.Policy;

/// <summary>The 997 passwords the legacy stack refuses as too common, from the same file.</summary>
internal static class CommonPasswords
{
    private const string ResourceName = "common-passwords.txt";

    private static readonly FrozenSet<string> Passwords = Load();

    // The list is lowercase: a password is compared lowercased, as mb_strtolower does.
    public static bool Contains(string password) =>
        Passwords.Contains(password.ToLowerInvariant());

    private static FrozenSet<string> Load()
    {
        using var stream = typeof(CommonPasswords).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        var passwords = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                passwords.Add(line);
            }
        }

        return passwords.ToFrozenSet(StringComparer.Ordinal);
    }
}
