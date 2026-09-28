namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// A dependency answered, but not with what <c>/v1</c> needs (a catalog the storage no
/// longer holds): the request gets the 503 of an outage.
/// </summary>
internal sealed class DependencyUnavailableException : Exception
{
    public DependencyUnavailableException()
    {
    }

    public DependencyUnavailableException(string message)
        : base(message)
    {
    }

    public DependencyUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
