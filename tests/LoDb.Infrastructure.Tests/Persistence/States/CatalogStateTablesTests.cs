using System.Globalization;
using LoDb.Domain.Catalog;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.States;

/// <summary>
/// The tables of the index of entity states: one open state per entity, rows the index could
/// not make sense of refused, states that follow their generation, and a state round-tripped.
/// </summary>
public sealed class CatalogStateTablesTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Fingerprint =
        "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private const string Open = "NULL, NULL";

    public static TheoryData<string, string> InvalidStates => new()
    {
        { Row("'spells'", Open, "NULL, false, '{}'"), "ck_catalog_entity_state_resource" },
        { Row("'items'", "'16.1.1', NULL", "NULL, false, '{}'"), "ck_catalog_entity_state_closed" },
        { Row("'items'", Open, "NULL, true, '{}'"), "ck_catalog_entity_state_return" },
        { Row("'items'", Open, "NULL, false, '{name}'"), "ck_catalog_entity_state_new" },
        { "'items', NULL, NULL, 'ABC', NULL, false, '{}'", "ck_catalog_entity_state_fingerprint" },
    };

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public async Task InvalidStateIsRefused(string values, string constraint)
    {
        var generation = await InsertGenerationAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertStateAsync(generation, "16.0.1", values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task EntityHasOneOpenStateAtMost()
    {
        var generation = await InsertGenerationAsync();
        var open = Row("'items'", Open, "'15.24.1', false, '{name}'");
        await InsertStateAsync(generation, "16.0.1", open);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertStateAsync(generation, "16.1.1", open));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_catalog_entity_state_open", error.ConstraintName);
    }

    [Fact]
    public async Task StateRoundTripsAndFollowsItsGeneration()
    {
        var generation = await InsertGenerationAsync();
        await InsertStateAsync(
            generation,
            "16.0.1",
            Row("'items'", "'16.2.1', now()", "'15.24.1', true, '{name,stat.health}'"));

        await using (var context = Database.CreateContext())
        {
            var state = await context.CatalogEntityStates.AsNoTracking().SingleAsync(Cancellation);
            Assert.Equal(
                (ResourceType.Items, "16.2.1", true, "name|stat.health"),
                (state.Resource, state.LastVersion, state.IsReturn,
                    string.Join('|', state.ChangedFields)));
        }

        await Database.ExecuteAsync("DELETE FROM catalog_state_generation", Cancellation);
        Assert.Equal(
            ["0"],
            await Database.QueryAsync("SELECT count(*) FROM catalog_entity_state", Cancellation));
    }

    // The values after the key: resource, then last_version and closed_at, the fingerprint,
    // then previous_opener_version, is_return and changed_fields.
    private static string Row(string resource, string closure, string lineage) =>
        $"{resource}, {closure}, '{Fingerprint}', {lineage}";

    private async Task<long> InsertGenerationAsync()
    {
        var ids = await Database.QueryAsync(
            "INSERT INTO catalog_state_generation (scheme, first_version)"
                + " VALUES (1, '16.0.1') RETURNING id",
            Cancellation);
        return long.Parse(ids[0], CultureInfo.InvariantCulture);
    }

    private Task InsertStateAsync(long generation, string opener, string values) =>
        Database.ExecuteAsync(
            "INSERT INTO catalog_entity_state (generation_id, entity_key, opener_version, path,"
                + " resource, last_version, closed_at, fingerprint, previous_opener_version,"
                + " is_return, changed_fields)"
                + $" VALUES ({generation}, '1036', '{opener}', 'items/1036-long-sword', {values})",
            Cancellation);
}
