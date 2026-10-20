namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// Scaleway gave no bill the panels can use: an error status, an answer that is not the list
/// of consumptions, or more pages than a bill of a few projects holds. Worth trying again
/// later.
/// </summary>
internal sealed class ScalewayBillingException : Exception
{
    public ScalewayBillingException(string message)
        : base(message)
    {
    }

    public ScalewayBillingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
