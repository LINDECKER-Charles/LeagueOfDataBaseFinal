namespace LoDb.Ingestion.Egress.Errors;

/// <summary>
/// A fetch that produced neither a body nor a definitive absence.
/// </summary>
/// <remarks>
/// Catching this base is enough for a caller that must persist nothing on failure: a
/// definitive absence is a <see cref="FetchOutcome.Absent"/> value, never an exception.
/// </remarks>
public abstract class EgressException : Exception
{
    protected EgressException(string message)
        : base(message)
    {
    }

    protected EgressException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
