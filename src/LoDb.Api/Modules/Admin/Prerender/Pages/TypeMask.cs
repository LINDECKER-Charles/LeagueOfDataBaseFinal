using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// A set of types of page as four bits, in the order of <see cref="ResourceType"/>: bit 0
/// champions, 1 items, 2 runes, 3 summoners. What the coverage of an epoch keeps per
/// (version, locale), and what tells two passes over the same types apart.
/// </summary>
internal static class TypeMask
{
    /// <summary>The four types: what a scope left null renders.</summary>
    public const int All = 0b1111;

    /// <summary>No type at all.</summary>
    public const int None = 0;

    private static readonly ResourceType[] Types = Enum.GetValues<ResourceType>();

    /// <summary>The bit of <paramref name="type"/>.</summary>
    public static int Of(ResourceType type) => 1 << Array.IndexOf(Types, type);

    /// <summary>The bits of <paramref name="types"/>; <see cref="All"/> when null.</summary>
    public static int Of(IReadOnlyList<ResourceType>? types) =>
        types is null ? All : types.Aggregate(None, static (mask, type) => mask | Of(type));

    /// <summary>
    /// The bits of the types <paramref name="scope"/> renders whole; <see cref="None"/> for an
    /// openers scope, which leaves most pages of each type out: its units never make an epoch
    /// hold a type, nor stand for a pass over it.
    /// </summary>
    public static int Of(PassScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Pages == PassPages.Openers ? None : Of(scope.PageTypes);
    }

    /// <summary>
    /// Whether <paramref name="mask"/> holds every bit of <paramref name="wanted"/>.
    /// </summary>
    public static bool Holds(int mask, int wanted) => (mask & wanted) == wanted;

    /// <summary>The bit of each type, in their order.</summary>
    public static IEnumerable<int> Bits() => Types.Select(static type => Of(type));
}
