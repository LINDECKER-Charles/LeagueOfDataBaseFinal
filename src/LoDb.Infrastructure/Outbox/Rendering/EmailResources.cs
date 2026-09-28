using System.Collections.Frozen;
using System.Reflection;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The templates (<c>Outbox/Templates/</c>) and text catalogs (<c>Outbox/Texts/</c>)
/// embedded in the assembly, read once.
/// </summary>
internal static class EmailResources
{
    private const string TemplatePrefix = "outbox.templates.";
    private const string TextPrefix = "outbox.texts.";
    private const string TextSuffix = ".json";

    private static readonly Assembly Assembly = typeof(EmailResources).Assembly;

    private static readonly FrozenDictionary<string, string> Templates =
        Assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(TemplatePrefix, StringComparison.Ordinal))
            .ToFrozenDictionary(static name => name[TemplatePrefix.Length..], Read);

    /// <summary>A template by file name (<c>account.html</c>).</summary>
    public static string Template(string fileName) =>
        Templates.TryGetValue(fileName, out var template)
            ? template
            : throw new InvalidOperationException($"No e-mail template named {fileName}.");

    /// <summary>The JSON text catalogs, by locale code (<c>en</c>, <c>fr</c>).</summary>
    public static IEnumerable<(string Code, string Json)> TextCatalogs() =>
        Assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(TextPrefix, StringComparison.Ordinal)
                && name.EndsWith(TextSuffix, StringComparison.Ordinal))
            .Select(static name =>
                (name[TextPrefix.Length..^TextSuffix.Length], Read(name)));

    private static string Read(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
