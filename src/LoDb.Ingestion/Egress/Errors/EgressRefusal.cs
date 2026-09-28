namespace LoDb.Ingestion.Egress.Errors;

/// <summary>
/// Why the allow-list refused a request.
/// </summary>
public enum EgressRefusal
{
    /// <summary>The URL is relative: the caller built something it must never build.</summary>
    NotAbsolute,

    /// <summary>Anything but https.</summary>
    Scheme,

    /// <summary>A host outside <c>LoDb:Egress:AllowedHosts</c>.</summary>
    Host,

    /// <summary>
    /// An allow-listed origin redirected to a target the allow-list refuses: a relay attempt,
    /// not a caller bug, hence a reason of its own.
    /// </summary>
    Redirect,
}
