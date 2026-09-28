using System.Globalization;
using System.Text;
using System.Text.Json;
using LoDb.Domain.Catalog.Items;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Normalization.Serialization;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// The stored JSON: a function of the content alone, compact and readable in every script.
/// </summary>
public sealed class DatasetSerializerTests
{
    [Fact]
    public async Task PropertiesAndMapKeysAreSortedAtEveryLevel()
    {
        var items = await ReadItemsAsync("en_US");

        using var json = JsonDocument.Parse(DatasetSerializer.Serialize(DatasetTypes.Items, items));

        AssertSorted(json.RootElement);
    }

    [Fact]
    public async Task InsertionOrderDoesNotChangeTheBytes()
    {
        var items = await ReadItemsAsync("en_US");
        var reversed = items with
        {
            Entries = [.. items.Entries.Select(static item => item with
            {
                Stats = item.Stats.Reverse().ToDictionary(),
                Maps = item.Maps.Reverse().ToDictionary(),
            })],
        };

        Assert.Equal(
            DatasetSerializer.Serialize(DatasetTypes.Items, items),
            DatasetSerializer.Serialize(DatasetTypes.Items, reversed));
    }

    // Edition and Charges are derived from stored fields: storing them could only go stale.
    [Fact]
    public async Task NullAndComputedMembersAreNotStored()
    {
        var items = await ReadItemsAsync("en_US");

        using var json = JsonDocument.Parse(DatasetSerializer.Serialize(DatasetTypes.Items, items));

        var classic = json.RootElement.GetProperty("entries").EnumerateArray()
            .Single(static item => item.GetProperty("id").GetString() == "771004");
        Assert.False(classic.TryGetProperty("edition", out _));
        Assert.False(classic.TryGetProperty("requiredChampion", out _));
        var twin = classic.GetProperty("counterpart");
        Assert.Equal("Modern", twin.GetProperty("edition").GetString());
    }

    [Fact]
    public async Task LettersOfEveryScriptAreWrittenAsIs()
    {
        var items = await ReadItemsAsync("ko_KR");
        var boots = Assert.Single(items.Entries, static item => item.Id == "1001");

        var stored = Encoding.UTF8.GetString(
            DatasetSerializer.Serialize(DatasetTypes.Items, items));

        Assert.Contains($"\"name\":\"{boots.Name}\"", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("<", stored, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyNamesTheStoredFile()
    {
        var scope = ReplayHarness.Scope("16.19.1", "fr_FR");

        Assert.Equal(
            "data/16.19.1/fr_FR/champions.json",
            scope.Key(DatasetTypes.Champions).RelativePath);
        Assert.Equal(
            "data/16.19.1/fr_FR/summoners.json",
            scope.Key(DatasetTypes.Summoners).RelativePath);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"version\":\"16.19.1\"}")]
    public async Task IncompleteDatasetIsRejected(string stored)
    {
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(stored));

        await Assert.ThrowsAsync<JsonException>(() => DatasetSerializer.DeserializeAsync(
            DatasetTypes.Runes, json, ReplayHarness.Token));
    }

    private static void AssertSorted(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = element.EnumerateObject().Select(static property => property.Name).ToList();
            Assert.Equal(names.Order(KeyOrder(names)), names);
        }

        var children = element.ValueKind switch
        {
            JsonValueKind.Object =>
                element.EnumerateObject().Select(static property => property.Value),
            JsonValueKind.Array => element.EnumerateArray(),
            _ => [],
        };
        foreach (var child in children)
        {
            AssertSorted(child);
        }
    }

    // Map ids ("11", "453") sort as numbers, every other key as ordinal text.
    private static IComparer<string> KeyOrder(List<string> names) =>
        names.All(static name => int.TryParse(name, CultureInfo.InvariantCulture, out _))
            ? Comparer<string>.Create(static (left, right) =>
                int.Parse(left, CultureInfo.InvariantCulture)
                    .CompareTo(int.Parse(right, CultureInfo.InvariantCulture)))
            : StringComparer.Ordinal;

    private static async Task<DatasetDocument<Item>> ReadItemsAsync(string language)
    {
        using var harness = ReplayHarness.Create();
        return await harness.Datasets.ReadItemsAsync(
            ReplayHarness.Scope(DdragonFixtures.Latest.Value, language), ReplayHarness.Token);
    }
}
