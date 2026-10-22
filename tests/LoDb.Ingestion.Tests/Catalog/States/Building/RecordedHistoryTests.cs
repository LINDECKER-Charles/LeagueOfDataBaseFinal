using LoDb.Domain.Catalog;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Catalog.States.Building;

/// <summary>
/// The whole recording walked by the index, en_US, from its oldest version to its newest: the
/// changes Data Dragon really made open states that name what changed, the newest patch,
/// which changes nothing a page shows, opens none, and an extension over real catalogs gives
/// what a rebuild gives.
/// </summary>
public sealed class RecordedHistoryTests(PostgresContainerFixture postgres)
{
    private const string FirstRunes = "7.22.1";
    private const string RenamedFiddlesticks = "7.21.1";
    private const string Boots = "1001";

    private static readonly string[] Recorded =
        [.. DdragonFixtures.Versions.Reverse().Select(static version => version.Value)];

    [Fact]
    public async Task EveryRecordedVersionIsIndexedWithTheFieldsItChanged()
    {
        await using var index = await RecordedAsync();
        await index.PromoteAsync(Recorded[^1]);

        await index.BuildAsync();
        var states = await index.StatesAsync();

        Assert.Equal(Recorded, await index.CoverageAsync());
        Assert.DoesNotContain(states, static state => state.OpenerVersion == Recorded[^1]);
        Assert.All(
            states.Where(static state => state.Resource == ResourceType.Runes),
            static state => Assert.True(Version(state.OpenerVersion) >= Version(FirstRunes)));
        Assert.All(states, AssertNamesWhatChanged);
        Assert.Equal(
            ["description"],
            Opened(states, ResourceType.Items, Boots, FirstRunes).ChangedFields);
    }

    // Data Dragon wrote FiddleSticks up to 3.13.24: one entity whose state changes there, each
    // state under the case of its opener.
    [Fact]
    public async Task AnIdWhoseCaseChangedKeepsOneHistory()
    {
        await using var index = await RecordedAsync();
        await index.PromoteAsync(Recorded[^1]);

        await index.BuildAsync();
        var states = await index.StatesAsync();
        var renamed = Opened(states, ResourceType.Champions, "fiddlesticks", RenamedFiddlesticks);

        Assert.Equal(
            "champions/FiddleSticks",
            states.First(static state => state.EntityKey == "fiddlesticks").Path);
        Assert.Equal(
            ("champions/Fiddlesticks", false),
            (renamed.Path, renamed.PreviousOpenerVersion is null));
        Assert.DoesNotContain("name", renamed.ChangedFields);
    }

    // The walk resumes from the stored states: open ones projected at the coverage end,
    // closed ones read back from their openers' catalogs when they come back.
    [Fact]
    public async Task AnExtensionOverTheRecordingGivesWhatARebuildGives()
    {
        await using var index = await RecordedAsync();
        await index.PromoteAsync(RenamedFiddlesticks);
        await index.BuildAsync();
        await index.PromoteAsync(Recorded[^1]);

        var extended = await index.BuildAsync();
        var walked = await index.StatesAsync();
        await index.RebuildAsync();
        var rebuilt = await index.StatesAsync();

        Assert.Equal(StateIndexOutcome.Extended, extended.Outcome);
        Assert.Equal(walked.Select(Comparable), rebuilt.Select(Comparable));
    }

    // Every listed version of the recording, stored as the ingestion stores its en_US datasets.
    private async Task<StateIndexHarness> RecordedAsync()
    {
        var index = await StateIndexHarness.CreateAsync(postgres, Recorded);
        using var replay = ReplayHarness.Create();
        foreach (var version in DdragonFixtures.Versions)
        {
            await index.StoreAsync(
                version.Value,
                await CatalogFixtures.DatasetsAsync(replay, version, CatalogFixtures.English));
        }

        return index;
    }

    // A changed state names at least one field, every one a key the API can read back; a new
    // entity names none, and a return names what differs from the state it left with.
    private static void AssertNamesWhatChanged(CatalogEntityState state)
    {
        Assert.All(state.ChangedFields, static key => Assert.True(FieldKey.TryParse(key, out _)));
        Assert.Equal(state.ChangedFields.Distinct(), state.ChangedFields);
        if (state.PreviousOpenerVersion is null)
        {
            Assert.Empty(state.ChangedFields);
        }
        else if (!state.IsReturn)
        {
            Assert.NotEmpty(state.ChangedFields);
        }
    }

    private static CatalogEntityState Opened(
        IEnumerable<CatalogEntityState> states,
        ResourceType resource,
        string key,
        string opener) =>
        Assert.Single(states, state =>
            state.Resource == resource && state.EntityKey == key && state.OpenerVersion == opener);

    private static PatchVersion Version(string value) => PatchVersion.Parse(value);

    private static string Comparable(CatalogEntityState state) =>
        string.Join(
            '|',
            state.Resource,
            state.EntityKey,
            state.OpenerVersion,
            state.LastVersion,
            state.ClosedAt,
            state.Fingerprint,
            state.Path,
            state.PreviousOpenerVersion,
            state.IsReturn,
            string.Join(',', state.ChangedFields));
}
