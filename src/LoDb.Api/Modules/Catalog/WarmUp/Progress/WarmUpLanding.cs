using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Catalog.WarmUp.Progress;

/// <summary>The entry whose image was fetched last, named in the requested language.</summary>
internal sealed record WarmUpLanding
{
    public required string Name { get; init; }

    public required ResourceType Resource { get; init; }
}
