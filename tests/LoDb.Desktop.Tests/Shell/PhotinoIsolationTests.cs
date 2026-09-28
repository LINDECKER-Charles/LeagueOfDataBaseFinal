namespace LoDb.Desktop.Tests.Shell;

/// <summary>ADR 0007: Photino is referenced by the implementation of IDesktopShell only.</summary>
public sealed class PhotinoIsolationTests
{
    private const string PhotinoFolder = "Shell/Photino/";

    [Fact]
    public void OnlyTheShellImplementationReferencesPhotino()
    {
        var source = Path.Combine(RepositoryRoot(), "src", "LoDb.Desktop");
        var referencing = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(source, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("obj/", StringComparison.Ordinal)
                && !path.StartsWith("bin/", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(Path.Combine(source, path))
                .Contains("Photino.NET", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(referencing);
        Assert.All(
            referencing,
            path => Assert.StartsWith(PhotinoFolder, path, StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoDb.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("LoDb.slnx not found.");
    }
}
