using System.Text.Json.Nodes;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Comparison;

/// <summary>
/// Compares two values of one entry down to the leaves and records every deviation at its
/// path: objects field by field, keyed lists element by element then for order, other lists
/// position by position, images by file then verdict.
/// </summary>
public sealed class NodeComparer(DeviationSite site, IReadOnlySet<string> tags)
{
    private readonly List<Deviation> found = [];

    public IReadOnlyList<Deviation> Found => found;

    public void Compare(JsonNode? legacy, JsonNode? next, string field)
    {
        switch (legacy, next)
        {
            case (JsonObject left, JsonObject right)
                when ImageVerdict.IsImage(left) && ImageVerdict.IsImage(right):
                CompareImages(left, right, field);
                break;
            case (JsonObject left, JsonObject right):
                CompareObjects(left, right, field);
                break;
            case (JsonArray left, JsonArray right):
                CompareLists(left, right, field);
                break;
            default:
                CompareLeaves(legacy, next, field);
                break;
        }
    }

    /// <summary>Elements one list holds and the other lacks, shared ones out of order, then
    /// each shared element.</summary>
    public void CompareKeyed(KeyedList legacy, KeyedList next, string field)
    {
        foreach (var (key, element) in legacy.Elements.Where(e => !next.ByKey.ContainsKey(e.Key)))
        {
            Add(DeviationKind.OnlyInLegacy, $"{field}[{key}]", (JsonValues.Render(element), null));
        }

        foreach (var (key, element) in next.Elements.Where(e => !legacy.ByKey.ContainsKey(e.Key)))
        {
            Add(DeviationKind.OnlyInNext, $"{field}[{key}]", (null, JsonValues.Render(element)));
        }

        var legacyOrder = legacy.SharedWith(next).ToList();
        var nextOrder = next.SharedWith(legacy).ToList();
        if (!legacyOrder.SequenceEqual(nextOrder))
        {
            var orders = (string.Join(',', legacyOrder), string.Join(',', nextOrder));
            Add(DeviationKind.Order, field, orders);
        }

        foreach (var key in legacyOrder.Distinct())
        {
            Compare(legacy.ByKey[key], next.ByKey[key], $"{field}[{key}]");
        }
    }

    private static string Join(string field, string name) =>
        field.Length == 0 ? name : $"{field}.{name}";

    private void CompareImages(JsonObject legacy, JsonObject next, string field)
    {
        Compare(legacy["file"], next["file"], Join(field, "file"));
        var (left, right) = (ImageVerdict.Of(legacy), ImageVerdict.Of(next));
        if (left != right)
        {
            Add(DeviationKind.Value, field, (left, right));
        }
    }

    private void CompareObjects(JsonObject legacy, JsonObject next, string field)
    {
        var names = legacy.Select(static p => p.Key).Union(next.Select(static p => p.Key));
        foreach (var name in names)
        {
            Compare(legacy[name], next[name], Join(field, name));
        }
    }

    private void CompareLists(JsonArray legacy, JsonArray next, string field)
    {
        if (KeyedList.TryPair(legacy, next, out var keyed))
        {
            CompareKeyed(keyed.Legacy, keyed.Next, field);
            return;
        }

        if (legacy.Count != next.Count)
        {
            CompareLeaves(legacy, next, field);
            return;
        }

        for (var index = 0; index < legacy.Count; index++)
        {
            Compare(legacy[index], next[index], $"{field}[{index}]");
        }
    }

    private void CompareLeaves(JsonNode? legacy, JsonNode? next, string field)
    {
        if (!JsonValues.Equal(legacy, next))
        {
            Add(DeviationKind.Value, field, (JsonValues.Render(legacy), JsonValues.Render(next)));
        }
    }

    private void Add(DeviationKind kind, string field, (string? Legacy, string? Next) values) =>
        found.Add(new Deviation
        {
            Site = site,
            Kind = kind,
            Field = field,
            Legacy = values.Legacy,
            Next = values.Next,
            Tags = tags,
        });
}
