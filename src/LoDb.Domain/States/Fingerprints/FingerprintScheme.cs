namespace LoDb.Domain.States.Fingerprints;

/// <summary>
/// The version of what a fingerprint covers: the projections, the order and patterns of the
/// field templates, the plain text form and the serialization.
/// </summary>
/// <remarks>
/// Stored with each generation of the index: the API reads only the generation of the scheme
/// it compiles, so a new scheme rebuilds the index next to the served one, and the pages whose
/// state changes are rendered again (a frozen page is frozen for one scheme).
/// </remarks>
public static class FingerprintScheme
{
    public const int Current = 1;
}
