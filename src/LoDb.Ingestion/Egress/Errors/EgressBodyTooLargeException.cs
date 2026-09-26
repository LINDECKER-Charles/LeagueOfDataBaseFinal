namespace LoDb.Ingestion.Egress.Errors;

/// <summary>
/// A response body over <c>LoDb:Egress:MaxResponseBytes</c>.
/// </summary>
/// <remarks>
/// Raised instead of truncating: a truncated body would be stored as a corrupt asset.
/// </remarks>
public sealed class EgressBodyTooLargeException : EgressException
{
    public EgressBodyTooLargeException(long maxBytes)
        : base($"Response body exceeds the cap of {maxBytes} bytes.")
    {
        MaxBytes = maxBytes;
    }

    public long MaxBytes { get; }
}
