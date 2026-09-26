using System.Text.Json;

namespace LoDb.Infrastructure.Tests.Analytics.Support;

/// <summary>
/// Compares two JSON documents as the reports mean them: a ranking (an array of objects with
/// a <c>name</c>) by name, since equal counts may come in either order; numbers to within a
/// rounding error; anything else exactly.
/// </summary>
internal static class JsonAssert
{
    private const string NameProperty = "name";
    private const double Tolerance = 1e-9;

    public static void Equivalent(JsonElement expected, JsonElement actual, string path = "$")
    {
        Assert.True(expected.ValueKind == actual.ValueKind, $"{path}: {actual.ValueKind}.");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                EquivalentObjects(expected, actual, path);
                break;
            case JsonValueKind.Array when IsRanking(expected):
                EquivalentRankings(expected, actual, path);
                break;
            case JsonValueKind.Array:
                EquivalentArrays(expected, actual, path);
                break;
            case JsonValueKind.Number:
                Assert.True(
                    Math.Abs(expected.GetDouble() - actual.GetDouble()) < Tolerance,
                    $"{path}: {actual} for {expected}.");
                break;
            default:
                Assert.True(expected.ToString() == actual.ToString(), $"{path}: {actual}.");
                break;
        }
    }

    private static void EquivalentObjects(JsonElement expected, JsonElement actual, string path)
    {
        Assert.Equal(
            expected.EnumerateObject().Select(static property => property.Name).Order(),
            actual.EnumerateObject().Select(static property => property.Name).Order());
        foreach (var property in expected.EnumerateObject())
        {
            Equivalent(
                property.Value,
                actual.GetProperty(property.Name),
                $"{path}.{property.Name}");
        }
    }

    private static void EquivalentArrays(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.GetArrayLength() == actual.GetArrayLength(), $"{path}: length.");
        var pairs = expected.EnumerateArray().Zip(actual.EnumerateArray());
        foreach (var (index, (left, right)) in pairs.Index())
        {
            Equivalent(left, right, $"{path}[{index}]");
        }
    }

    // Same entries by name, and the actual one in decreasing order of count.
    private static void EquivalentRankings(JsonElement expected, JsonElement actual, string path)
    {
        var byName = actual.EnumerateArray().ToDictionary(NameOf);
        Assert.Equal(
            expected.EnumerateArray().Select(NameOf).Order(StringComparer.Ordinal),
            byName.Keys.Order(StringComparer.Ordinal));
        foreach (var entry in expected.EnumerateArray())
        {
            Equivalent(entry, byName[NameOf(entry)], $"{path}[{NameOf(entry)}]");
        }

        var counts = actual.EnumerateArray().Select(static entry => entry.GetProperty("count"))
            .Select(static count => count.GetInt64()).ToList();
        Assert.Equal(counts.OrderDescending(), counts);
    }

    private static bool IsRanking(JsonElement array) =>
        array.GetArrayLength() > 0
        && array.EnumerateArray().All(static entry =>
            entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty(NameProperty, out _));

    private static string NameOf(JsonElement entry) =>
        entry.GetProperty(NameProperty).GetString()!;
}
