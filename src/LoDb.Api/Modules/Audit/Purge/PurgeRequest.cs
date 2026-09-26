namespace LoDb.Api.Modules.Audit.Purge;

/// <summary>Body of <c>POST /api/admin/audit/purge</c>.</summary>
internal sealed record PurgeRequest
{
    /// <summary>
    /// <c>retention</c> (what the daily retention would delete now), <c>before</c> (every
    /// entry before <see cref="Before"/>) or <c>all</c>.
    /// </summary>
    public required string? Scope { get; init; }

    /// <summary>First UTC day kept, required by the <c>before</c> scope.</summary>
    public DateOnly? Before { get; init; }
}
