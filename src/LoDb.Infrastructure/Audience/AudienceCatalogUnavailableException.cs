namespace LoDb.Infrastructure.Audience;

/// <summary>
/// The catalog could not tell which entity a pulled page names (Data Dragon unreachable, no
/// version ingested yet): a transient failure, so the pull stops before it rewrites a day and
/// the figures already stored stay as they were.
/// </summary>
internal sealed class AudienceCatalogUnavailableException(string message) : Exception(message);
