namespace LoDb.Desktop.Launch;

/// <summary>
/// Validates an API origin given on the command line or in the environment, to point a
/// development build at another API.
/// </summary>
internal static class ApiOrigins
{
    /// <exception cref="ArgumentException">
    /// Not an https origin, nor an http one on loopback.
    /// </exception>
    public static Uri Parse(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin)
            || !IsAllowedScheme(origin)
            || !string.IsNullOrEmpty(origin.UserInfo)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment))
        {
            throw new ArgumentException(
                $"The API origin must be an https origin (http on loopback only): '{value}'.",
                nameof(value));
        }

        return new Uri(origin.GetLeftPart(UriPartial.Authority));
    }

    // The proxy adds the bearer token to every request: never over cleartext off this machine.
    private static bool IsAllowedScheme(Uri origin) =>
        origin.Scheme == Uri.UriSchemeHttps
        || (origin.Scheme == Uri.UriSchemeHttp && origin.IsLoopback);
}
