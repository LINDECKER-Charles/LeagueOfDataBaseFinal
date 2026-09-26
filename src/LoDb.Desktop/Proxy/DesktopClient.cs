namespace LoDb.Desktop.Proxy;

/// <summary>
/// The <c>X-LoDb-Client</c> header of the app (ADR 0008): the API reads it to answer
/// <c>426</c> below the minimum version.
/// </summary>
internal static class DesktopClient
{
    public const string HeaderName = "X-LoDb-Client";

    private const string Platform = "desktop";

    public static string ValueOf(string version) => $"{Platform}/{version}";
}
