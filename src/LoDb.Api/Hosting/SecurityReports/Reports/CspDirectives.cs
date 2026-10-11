using System.Collections.Frozen;

namespace LoDb.Api.Hosting.SecurityReports.Reports;

/// <summary>
/// The directives a report may name, as a bounded vocabulary: any other value, written by a
/// browser or by anyone posting a report, is counted as <see cref="Other"/>.
/// </summary>
internal static class CspDirectives
{
    public const string Other = "other";

    private static readonly FrozenSet<string> Known = new[]
    {
        "base-uri",
        "child-src",
        "connect-src",
        "default-src",
        "font-src",
        "form-action",
        "frame-ancestors",
        "frame-src",
        "img-src",
        "manifest-src",
        "media-src",
        "object-src",
        "require-trusted-types-for",
        "script-src",
        "script-src-attr",
        "script-src-elem",
        "style-src",
        "style-src-attr",
        "style-src-elem",
        "trusted-types",
        "worker-src",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The directive's name, or <see cref="Other"/>. Older browsers write the violated
    /// directive with its sources ("script-src 'self'"): only its first word counts.
    /// </summary>
    public static string Of(string? value)
    {
        var name = value?.Trim().Split(' ', 2)[0].ToLowerInvariant();
        return name is not null && Known.Contains(name) ? name : Other;
    }
}
