using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// An upstream document that answered but cannot be used: unreadable JSON, or a document
/// the source cannot do without (<c>versions.json</c>) reported absent.
/// </summary>
/// <remarks>
/// An <see cref="EgressException"/>: like a transient failure, it persists nothing, so a
/// caller catching that base covers every failure it must not record.
/// </remarks>
public sealed class UpstreamDocumentException : EgressException
{
    public UpstreamDocumentException(string message, Uri url)
        : base(message)
    {
        Url = url;
    }

    public UpstreamDocumentException(string message, Uri url, Exception innerException)
        : base(message, innerException)
    {
        Url = url;
    }

    /// <summary>The document at fault.</summary>
    public Uri Url { get; }
}
