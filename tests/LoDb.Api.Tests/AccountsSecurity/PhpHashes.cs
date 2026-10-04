using System.Text.Json;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// <c>tests/fixtures/hashes/</c>: hashes made by PHP 8.5 the way the legacy stack makes them
/// (<c>generate.sh</c>), the passwords they hash and the script that checks ours.
/// </summary>
internal static class PhpHashes
{
    public static string Directory { get; } =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hashes");

    private static readonly HashFile Fixtures = Load();

    /// <summary>The PHP version that made the hashes.</summary>
    public static string PhpVersion => Fixtures.Php;

    /// <summary>The passwords of <c>cases.php</c>, by name.</summary>
    public static IReadOnlyDictionary<string, string> Passwords => Fixtures.Passwords;

    /// <summary>The formats of <c>generate.php</c>, in its order.</summary>
    public static IReadOnlyList<string> Formats { get; } =
        [.. Fixtures.Cases.Select(static hash => hash.Format).Distinct()];

    /// <summary>Every format and password name, one hash each.</summary>
    public static IEnumerable<(string Format, string Password)> Cases =>
        Fixtures.Cases.Select(static hash => (hash.Format, hash.Password));

    /// <summary>The hash of the password named <paramref name="password"/>.</summary>
    public static string Hash(string format, string password) =>
        Fixtures.Cases.Single(hash => hash.Format == format && hash.Password == password).Hash;

    private static HashFile Load()
    {
        using var file = File.OpenRead(Path.Combine(Directory, "hashes.json"));
        return JsonSerializer.Deserialize<HashFile>(file, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("hashes.json is empty.");
    }

    private sealed record HashFile(
        string Php,
        Dictionary<string, string> Passwords,
        IReadOnlyList<PhpHash> Cases);

    private sealed record PhpHash(string Format, string Password, string Hash);
}
