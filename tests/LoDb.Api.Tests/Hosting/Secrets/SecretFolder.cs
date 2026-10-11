namespace LoDb.Api.Tests.Hosting.Secrets;

/// <summary>
/// A folder of secret files, as Compose mounts them in <c>/run/secrets</c>: one file per
/// setting, named after its key with <c>__</c> between the sections. Deleted with the
/// instance.
/// </summary>
public sealed class SecretFolder : IDisposable
{
    private const string FolderPrefix = "lodb-secret-files-";
    private const string KeyDelimiter = ":";
    private const string FileDelimiter = "__";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory(FolderPrefix);

    public string Path => _folder.FullName;

    /// <summary>Writes the file of the setting <paramref name="key"/>.</summary>
    public SecretFolder With(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return WithFile(key.Replace(KeyDelimiter, FileDelimiter, StringComparison.Ordinal), value);
    }

    /// <summary>Writes a file of any name, a setting or not.</summary>
    public SecretFolder WithFile(string name, string value)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, name), value);
        return this;
    }

    public void Dispose() => _folder.Delete(recursive: true);
}
