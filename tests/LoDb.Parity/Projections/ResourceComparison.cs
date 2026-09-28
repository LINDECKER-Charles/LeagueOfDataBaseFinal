using System.Text.Json.Nodes;
using LoDb.Parity.Comparison;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Projections;

/// <summary>One resource of a <see cref="ProjectionPair"/>, compared.</summary>
internal sealed class ResourceComparison
{
    // The legacy export leaves out the canonical path, a new URL scheme (ADR 0005) the legacy
    // stack has no equivalent of, and says whether it holds a champion's detail.
    private static readonly string[] NotProjected = ["path", "detail"];

    // Only stored by the legacy stack when a detail page is visited.
    private static readonly string[] DetailFields = ["passive", "spells", "skins"];

    private readonly DeviationSite site;
    private readonly (string? Legacy, string? Next) contents;
    private readonly (KeyedList Legacy, KeyedList Next) entries;
    private readonly bool fallback;
    private readonly IReadOnlySet<string> placeholders;
    private readonly List<Deviation> found = [];

    public ResourceComparison(
        DeviationSite site,
        JsonNode? legacy,
        JsonNode? next,
        IReadOnlySet<string> placeholders)
    {
        this.site = site;
        this.placeholders = placeholders;
        contents = (Language(legacy), Language(next));
        entries = (KeyedList.Of(Entries(legacy)), KeyedList.Of(Entries(next)));
        fallback = contents.Legacy != site.Language || contents.Next != site.Language;
    }

    public List<Deviation> Deviations()
    {
        if (contents.Legacy != contents.Next)
        {
            Add(DeviationKind.ContentLanguage, "contentLanguage", contents);
        }

        CompareMembership();
        CompareOrder();
        foreach (var id in entries.Legacy.SharedWith(entries.Next).Distinct())
        {
            CompareEntry(id, entries.Legacy.ByKey[id], entries.Next.ByKey[id]);
        }

        return found;
    }

    private static string? Language(JsonNode? resource) =>
        resource?["contentLanguage"]?.GetValue<string>();

    private static JsonArray Entries(JsonNode? resource) =>
        resource?["entries"] as JsonArray ?? [];

    private static IEnumerable<string> FieldsOf(JsonObject legacy, JsonObject next)
    {
        var detailed = legacy["detail"] is not JsonValue detail || detail.GetValue<bool>();
        return legacy.Select(static p => p.Key)
            .Union(next.Select(static p => p.Key))
            .Except(NotProjected)
            .Where(field => detailed || !DetailFields.Contains(field));
    }

    private void CompareMembership()
    {
        foreach (var (id, entry) in entries.Legacy.Elements)
        {
            if (!entries.Next.ByKey.ContainsKey(id))
            {
                AddEntry(DeviationKind.OnlyInLegacy, id, (entry, null));
            }
        }

        foreach (var (id, entry) in entries.Next.Elements)
        {
            if (!entries.Legacy.ByKey.ContainsKey(id))
            {
                AddEntry(DeviationKind.OnlyInNext, id, (null, entry));
            }
        }
    }

    private void CompareOrder()
    {
        var legacyOrder = entries.Legacy.SharedWith(entries.Next).ToList();
        var nextOrder = entries.Next.SharedWith(entries.Legacy).ToList();
        if (!legacyOrder.SequenceEqual(nextOrder))
        {
            Add(DeviationKind.Order, "entries", (Joined(legacyOrder), Joined(nextOrder)));
        }
    }

    private void CompareEntry(string id, JsonObject legacy, JsonObject next)
    {
        var tags = TagsOf(id, legacy, next);
        var comparer = new NodeComparer(site with { Entry = id }, tags);
        foreach (var field in FieldsOf(legacy, next))
        {
            comparer.Compare(legacy[field], next[field], field);
        }

        found.AddRange(comparer.Found);
    }

    private static string Joined(List<string> ids) =>
        JsonValues.Render(new JsonArray([.. ids.Select(static id => (JsonNode)id)]));

    private void Add(DeviationKind kind, string field, (string? Legacy, string? Next) values) =>
        found.Add(new Deviation
        {
            Site = site,
            Kind = kind,
            Field = field,
            Legacy = values.Legacy,
            Next = values.Next,
            Tags = EntryTags.Of(null, null, fallback),
        });

    private void AddEntry(
        DeviationKind kind,
        string id,
        (JsonObject? Legacy, JsonObject? Next) sides) =>
        found.Add(new Deviation
        {
            Site = site with { Entry = id },
            Kind = kind,
            Legacy = sides.Legacy is null ? null : JsonValues.Render(sides.Legacy),
            Next = sides.Next is null ? null : JsonValues.Render(sides.Next),
            Tags = TagsOf(id, sides.Legacy, sides.Next),
        });

    private IReadOnlySet<string> TagsOf(string id, JsonObject? legacy, JsonObject? next)
    {
        var tags = EntryTags.Of(legacy, next, fallback);
        return placeholders.Contains(id)
            ? new HashSet<string>(tags, StringComparer.Ordinal) { DeviationTags.Placeholder }
            : tags;
    }
}
