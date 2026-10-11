namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// Whether the browser blocked what it reports (<see cref="Enforce"/>) or only reported it,
/// under a <c>Content-Security-Policy-Report-Only</c> header (<see cref="Report"/>).
/// </summary>
internal static class CspDispositions
{
    public const string Enforce = "enforce";
    public const string Report = "report";

    /// <summary>A report without its disposition, as older browsers write them.</summary>
    public const string Unknown = "unknown";

    public static string Of(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            Enforce => Enforce,
            Report => Report,
            _ => Unknown,
        };
}
