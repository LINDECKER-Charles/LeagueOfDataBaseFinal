namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// What one run of a periodic job processed, for its summary line: <c>3 versions</c>,
/// <c>1200 rows</c>.
/// </summary>
public sealed record JobRunSummary(long Count, string Unit);
