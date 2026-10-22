using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.States;

/// <summary>
/// Stored spelling of the resource of a state: the first segment of its paths, which the
/// check constraint of <c>catalog_entity_state</c> holds to.
/// </summary>
public static class CatalogStateColumns
{
    /// <summary>The longest resource segment (<c>summoners</c>).</summary>
    public const int ResourceMaxLength = 16;

    public static readonly ValueConverter<ResourceType, string> ResourceConverter = new(
        resource => CanonicalPath.SegmentOf(resource),
        value => ParseResource(value));

    public static ResourceType ParseResource(string value) =>
        Enum.GetValues<ResourceType>()
            .Where(resource => CanonicalPath.SegmentOf(resource) == value)
            .Select(static resource => (ResourceType?)resource)
            .FirstOrDefault()
            ?? throw new ArgumentOutOfRangeException(nameof(value), value, null);
}
