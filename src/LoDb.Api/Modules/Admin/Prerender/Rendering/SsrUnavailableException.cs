namespace LoDb.Api.Modules.Admin.Prerender.Rendering;

/// <summary>
/// The SSR gave no answer the pass can use: out of reach, too slow, or answering a build's
/// list or file with something else than it. Worth trying again later.
/// </summary>
internal sealed class SsrUnavailableException : Exception
{
    public SsrUnavailableException(string message)
        : base(message)
    {
    }

    public SsrUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Whether the SSR could not be reached at all (connection refused, name not resolved): a
    /// server that restarts or is redeployed, not a page that fails.
    /// </summary>
    public bool IsUnreachable { get; init; }
}
