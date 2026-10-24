using System.Globalization;

namespace LoDb.Infrastructure.Robots.Reports.Folding;

/// <summary>How the reports group the statuses robots were answered with.</summary>
internal static class StatusClasses
{
    public const int Success = 2;
    public const int Redirect = 3;
    public const int ClientError = 4;
    public const int ServerError = 5;
    private const int ClassWidth = 100;
    private const short Forbidden = 403;
    private const short TooManyRequests = 429;
    private const short Unavailable = 503;

    /// <summary>The classes a chart draws, in order.</summary>
    public static IReadOnlyList<int> Classes { get; } =
        [Success, Redirect, ClientError, ServerError];

    public static int ClassOf(short status) => status / ClassWidth;

    /// <summary><c>2xx</c> for 2.</summary>
    public static string NameOf(int statusClass) =>
        string.Create(CultureInfo.InvariantCulture, $"{statusClass}xx");

    /// <summary>
    /// A refusal: a guard (403), a rate limit (429) or the shedding of renders (503), never a
    /// failure of the page itself.
    /// </summary>
    public static bool IsRefusal(short status) =>
        status is Forbidden or TooManyRequests or Unavailable;
}
