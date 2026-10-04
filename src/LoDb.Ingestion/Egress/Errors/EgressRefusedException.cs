namespace LoDb.Ingestion.Egress.Errors;

/// <summary>
/// A request the allow-list refused before any byte left the process.
/// </summary>
/// <remarks>
/// Never retried and never an absence: the URL is either a bug of the caller or an
/// upstream trying to relay the fetch elsewhere.
/// </remarks>
public sealed class EgressRefusedException : EgressException
{
    public EgressRefusedException(EgressRefusal reason, string? host)
        : base($"Egress refused ({reason}).")
    {
        Reason = reason;
        Host = host;
    }

    public EgressRefusal Reason { get; }

    /// <summary>
    /// The refused host, null when the URL has none.
    /// </summary>
    public string? Host { get; }
}
