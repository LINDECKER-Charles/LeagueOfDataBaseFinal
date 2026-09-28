namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>The counters above the build list, over every build.</summary>
internal sealed record AdminBuildStats
{
    public required int Total { get; init; }

    public required int Public { get; init; }
}
