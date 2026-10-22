using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Ingestion.Tests.Catalog.States;

/// <summary>
/// The reader of the stored index: the current generation of the compiled scheme alone, its
/// covered versions in order and its states as stored; empty while none is current.
/// </summary>
public sealed class StoredEntityStateIndexTests(PostgresContainerFixture postgres)
{
    private const string Fingerprint =
        "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NoCurrentGenerationReadsEmpty()
    {
        await using var database = await postgres.CreateMigratedDatabaseAsync(Cancellation);
        await database.ExecuteAsync(
            "INSERT INTO catalog_state_generation (scheme, first_version, is_current)"
                + " VALUES (1, '90.1.1', false), (2, '90.1.1', true)",
            Cancellation);

        var snapshot = await new StoredEntityStateIndex(new Contexts(database))
            .GetAsync(Cancellation);

        Assert.Same(EntityStateSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task CurrentGenerationReadsWithItsVersionsAndStates()
    {
        await using var database = await postgres.CreateMigratedDatabaseAsync(Cancellation);
        await database.ExecuteAsync(
            """
            INSERT INTO catalog_state_generation (id, scheme, first_version, last_version,
                is_current) OVERRIDING SYSTEM VALUE VALUES (5, 1, '90.9.1', '90.10.1', true);
            INSERT INTO catalog_state_version (generation_id, version, ordinal)
                VALUES (5, '90.10.1', 1), (5, '90.9.1', 0);
            """
                + "INSERT INTO catalog_entity_state (generation_id, resource, entity_key,"
                + " opener_version, fingerprint, path, previous_opener_version, changed_fields)"
                + $" VALUES (5, 'champions', 'fiddlesticks', '90.10.1', '{Fingerprint}',"
                + " 'champions/Fiddlesticks', '90.9.1', '{spell.q.cooldown}')",
            Cancellation);

        var snapshot = await new StoredEntityStateIndex(new Contexts(database))
            .GetAsync(Cancellation);
        var state = Assert.Single(snapshot.States);

        Assert.Equal(new StateIndexStamp(5, PatchVersion.Parse("90.10.1")), snapshot.Stamp);
        Assert.Equal("90.9.1", snapshot.Coverage.Start?.Value);
        Assert.Equal(
            ("fiddlesticks", "90.10.1", true, FieldKey.Of(ChangeField.SpellCooldown, "q")),
            (state.Key, state.Opener.Value, state.IsOpen, state.ChangedFields[0]));
    }

    private sealed class Contexts(TestDatabase database) : IDbContextFactory<LoDbDbContext>
    {
        public LoDbDbContext CreateDbContext() => database.CreateContext();
    }
}
