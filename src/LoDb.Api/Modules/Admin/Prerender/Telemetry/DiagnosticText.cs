namespace LoDb.Api.Modules.Admin.Prerender.Telemetry;

/// <summary>
/// A reason as the admin keeps it next to a unit or a pass: at most so many characters, an
/// ellipsis marking the cut, so that a long answer of the SSR or the store never refuses the
/// write that records it.
/// </summary>
internal static class DiagnosticText
{
    private const string Ellipsis = "…";

    /// <summary>
    /// <paramref name="text"/> cut to <paramref name="maxLength"/>; null stays null.
    /// </summary>
    public static string? Clip(string? text, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, Ellipsis.Length);
        return text is null || text.Length <= maxLength
            ? text
            : string.Concat(text.AsSpan(0, maxLength - Ellipsis.Length), Ellipsis);
    }
}
