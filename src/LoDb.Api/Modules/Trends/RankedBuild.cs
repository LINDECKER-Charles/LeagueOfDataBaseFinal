using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Trends;

/// <summary>A public build as the trends rank it: with its author and its net score.</summary>
internal sealed record RankedBuild
{
    public required Build Build { get; init; }

    public required User Owner { get; init; }

    public required int Score { get; init; }
}
