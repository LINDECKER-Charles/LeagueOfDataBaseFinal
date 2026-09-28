using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Storage;

/// <summary>
/// The configured storage directory, and the only place that turns a relative storage path
/// into a file system path.
/// </summary>
internal sealed class StorageRoot(IOptions<StorageOptions> options)
{
    private const string StagingFileFormat = "N";

    /// <summary>Absolute path of the root, without a trailing separator.</summary>
    public string FullPath { get; } =
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.Root));

    /// <summary>
    /// File system path of a relative storage path, refused if it resolves outside the root.
    /// </summary>
    /// <remarks>
    /// Callers validate their segments first; this check is the last line of defence.
    /// </remarks>
    public string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(FullPath, relativePath));
        if (!full.StartsWith(FullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{relativePath}' resolves outside the storage root.",
                nameof(relativePath));
        }

        return full;
    }

    /// <summary>
    /// A fresh, unique file name in the staging area, creating the area if needed.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The root itself does not exist.</exception>
    public string NewStagingPath()
    {
        // Creating the root here would hide an unmounted volume behind a container-local
        // directory: its absence is a deployment error, reported as such.
        if (!Directory.Exists(FullPath))
        {
            throw new DirectoryNotFoundException(
                $"The storage root '{FullPath}' does not exist.");
        }

        var staging = Directory.CreateDirectory(
            Path.Combine(FullPath, StorageLayout.StagingDirectory));
        return Path.Combine(staging.FullName, Guid.NewGuid().ToString(StagingFileFormat));
    }
}
