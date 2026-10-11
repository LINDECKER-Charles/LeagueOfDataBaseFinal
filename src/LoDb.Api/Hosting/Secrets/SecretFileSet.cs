using System.Text.RegularExpressions;

namespace LoDb.Api.Hosting.Secrets;

/// <summary>
/// The settings a folder of secret files holds: one file per setting, named after its key
/// with <c>__</c> between the sections, the way Compose mounts its secrets in
/// <c>/run/secrets</c>.
/// </summary>
/// <remarks>
/// An empty file is an unset setting: the deploy job writes a file for every secret the stack
/// declares, empty when the environment leaves it unset. A file whose name is no setting key
/// is no setting either: the Data Protection certificate shares the folder.
/// </remarks>
/// <param name="Folder">Absolute path of the folder.</param>
/// <param name="FileNames">Names of the setting files, in ordinal order.</param>
internal sealed partial record SecretFileSet(string Folder, IReadOnlyList<string> FileNames)
{
    /// <summary>Folder of the secret files; Compose's own when unset.</summary>
    public const string FolderKey = "LoDb:Hosting:SecretsDirectory";

    public const string DefaultFolder = "/run/secrets";

    /// <summary>Between two sections of a key, in a file name.</summary>
    public const string SectionDelimiter = "__";

    /// <summary>The configuration keys the files set, in the order of the files.</summary>
    public IEnumerable<string> SettingKeys =>
        FileNames.Select(static name => name.Replace(
            SectionDelimiter,
            ConfigurationPath.KeyDelimiter,
            StringComparison.Ordinal));

    /// <summary>The setting files of the folder <see cref="FolderKey"/> names.</summary>
    public static SecretFileSet Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration[FolderKey];
        var folder = new DirectoryInfo(
            string.IsNullOrWhiteSpace(configured) ? DefaultFolder : configured);
        if (!folder.Exists)
        {
            return new SecretFileSet(folder.FullName, []);
        }

        var names = folder.EnumerateFiles()
            .Where(static file => file.Length > 0 && SettingFileName().IsMatch(file.Name))
            .Select(static file => file.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        return new SecretFileSet(folder.FullName, names);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*(__[A-Za-z0-9]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex SettingFileName();
}
