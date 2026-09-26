namespace LoDb.Desktop.Tests.Support;

/// <summary>A temporary folder of one test, deleted with it.</summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder() => Path = Directory.CreateTempSubdirectory("lodb-desktop-").FullName;

    public string Path { get; }

    public string Combine(string relativePath) => System.IO.Path.Combine(Path, relativePath);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A file still held by the OS: the temp folder is cleaned eventually.
        }
    }
}
