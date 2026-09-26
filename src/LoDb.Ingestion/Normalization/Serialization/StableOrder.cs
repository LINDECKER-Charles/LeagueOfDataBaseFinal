using System.Text.Json.Serialization.Metadata;

namespace LoDb.Ingestion.Normalization.Serialization;

/// <summary>
/// Contract modifier that makes a dataset's bytes a function of its content alone:
/// properties in ordinal name order, maps in key order.
/// </summary>
/// <remarks>
/// Declaration order would tie the stored bytes to the source code, and a map's order to
/// whoever built it. Stable bytes let two ingestions of the same upstream be compared.
/// </remarks>
internal static class StableOrder
{
    public static void Apply(JsonTypeInfo contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        var ordered = contract.Properties
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToList();
        for (var order = 0; order < ordered.Count; order++)
        {
            ordered[order].Order = order;
            SortMap(ordered[order]);
        }
    }

    // The two map shapes of the domain: stats by name, availability by map id.
    private static void SortMap(JsonPropertyInfo property)
    {
        if (property.Get is not { } read)
        {
            return;
        }

        if (property.PropertyType == typeof(IReadOnlyDictionary<string, double>))
        {
            property.Get = owner =>
                Sorted(read(owner) as IReadOnlyDictionary<string, double>, StringComparer.Ordinal);
        }
        else if (property.PropertyType == typeof(IReadOnlyDictionary<int, bool>))
        {
            property.Get = owner =>
                Sorted(read(owner) as IReadOnlyDictionary<int, bool>, Comparer<int>.Default);
        }
    }

    private static SortedDictionary<TKey, TValue>? Sorted<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue>? map,
        IComparer<TKey> comparer)
        where TKey : notnull
    {
        if (map is null)
        {
            return null;
        }

        var sorted = new SortedDictionary<TKey, TValue>(comparer);
        foreach (var (key, value) in map)
        {
            sorted.Add(key, value);
        }

        return sorted;
    }
}
