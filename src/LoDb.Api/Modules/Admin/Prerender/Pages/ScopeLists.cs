using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The normal form of the lists of a scope, so that the stored scope stays small and "every
/// one of them" reads the same however it was asked: each item once, in the site's order, and
/// null for a list that holds them all.
/// </summary>
/// <remarks>
/// The versions are not normalised: the older patches change at each promotion, so only the
/// client may say "every one of them".
/// </remarks>
internal static class ScopeLists
{
    private static readonly ResourceType[] AllTypes = Enum.GetValues<ResourceType>();

    /// <summary>The locales in the order of <see cref="UiLocales.All"/>; null for all 21.</summary>
    public static IReadOnlyList<UiLocale>? LocalesOf(IEnumerable<UiLocale> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);
        var kept = locales.ToHashSet();
        return kept.Count == UiLocales.All.Count ? null : [.. UiLocales.All.Where(kept.Contains)];
    }

    /// <summary>The types in the order of <see cref="ResourceType"/>; null for all four.</summary>
    public static IReadOnlyList<ResourceType>? TypesOf(IEnumerable<ResourceType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var kept = types.ToHashSet();
        return kept.Count == AllTypes.Length ? null : [.. AllTypes.Where(kept.Contains)];
    }
}
