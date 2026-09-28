namespace LoDb.Desktop.Tests.Support;

/// <summary>A minimal shell build: an index and one script, as Angular emits them.</summary>
internal static class TestShell
{
    public const string Index =
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>LoDb</title>"
        + "</head><body><app-root></app-root><script src=\"main.js\"></script></body></html>";

    public const string Script = "console.log('shell');";

    public static string CreateIn(TestFolder folder)
    {
        var directory = folder.Combine("shell");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.html"), Index);
        File.WriteAllText(Path.Combine(directory, "main.js"), Script);
        return directory;
    }
}
