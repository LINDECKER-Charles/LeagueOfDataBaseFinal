namespace LoDb.Ingestion.Catalog.States;

/// <summary>
/// The index of states could not be read and no copy of it is held: transient, worth a retry
///. An index not built yet is no failure but an empty snapshot.
/// </summary>
public sealed class StateIndexUnavailableException : Exception
{
    public StateIndexUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
