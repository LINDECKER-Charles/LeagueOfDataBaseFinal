namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>A page of the builds matching a search, newest first.</summary>
internal sealed record AdminBuildPage
{
    public required AdminBuildStats Stats { get; init; }

    public required IReadOnlyList<AdminBuildRow> Items { get; init; }

    /// <summary>Builds matching the search and the visibility.</summary>
    public required int Total { get; init; }

    /// <summary>The page, from 1.</summary>
    public required int Page { get; init; }

    public required int Pages { get; init; }
}
