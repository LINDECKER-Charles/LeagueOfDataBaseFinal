namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// Limits of one streamed body: size cap, and deadline counted from the response headers.
/// </summary>
internal sealed record BodyPolicy(long MaxBytes, TimeSpan ReadTimeout, TimeProvider Time);
