using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Prerender;

/// <summary>
/// The tables of the pre-rendering passes: one active pass at a time, rows the engine could
/// not make sense of refused, units that follow their pass, and the entities round-tripped.
/// </summary>
public sealed class PrerenderTablesTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
    private const long ActiveSeconds = 3_600;

    public static TheoryData<string, string> InvalidPasses => new()
    {
        { "0, 'running', '{}', NULL, now()", "ck_prerender_pass_epoch" },
        { "1, 'stopped', '{}', NULL, NULL", "ck_prerender_pass_state" },
        { "1, 'running', '[]', NULL, now()", "ck_prerender_pass_scope" },
        { "1, 'running', '{}', now(), now()", "ck_prerender_pass_finished" },
        { "1, 'completed', '{}', NULL, NULL", "ck_prerender_pass_finished" },
        { "1, 'running', '{}', NULL, NULL", "ck_prerender_pass_running" },
        { "1, 'paused', '{}', NULL, now()", "ck_prerender_pass_running" },
    };

    [Fact]
    public async Task SecondActivePassIsRefusedWhateverItsState()
    {
        await InsertPassAsync("1, 'running', '{}', NULL, now()");

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertPassAsync("2, 'paused', '{}', NULL, NULL"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_prerender_pass_active", error.ConstraintName);
    }

    [Fact]
    public async Task PassesThatAreOverDoNotBlockANewOne()
    {
        await InsertPassAsync("1, 'completed', '{}', now(), NULL");
        await InsertPassAsync("2, 'cancelled', '{}', now(), NULL");

        await InsertPassAsync("3, 'running', '{}', NULL, now()");

        Assert.Equal(
            ["3"],
            await Database.QueryAsync("SELECT count(*) FROM prerender_pass", Cancellation));
    }

    [Theory]
    [MemberData(nameof(InvalidPasses))]
    public async Task InvalidPassIsRefused(string values, string constraint)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertPassAsync(values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task UnitsAreKeyedByPassVersionAndLocaleAndFollowTheirPass()
    {
        await using var context = Database.CreateContext();
        var pass = NewPass();
        pass.Units.Add(NewUnit("16.20.1", "fr-FR"));
        pass.Units.Add(NewUnit("16.20.1", "en-US"));
        context.PrerenderPasses.Add(pass);
        await context.SaveChangesAsync(Cancellation);

        Assert.True(pass.IsActive);
        Assert.Equal(2, await context.PrerenderUnits.CountAsync(Cancellation));
        await using (var other = Database.CreateContext())
        {
            other.PrerenderUnits.Add(NewUnit("16.20.1", "fr-FR", pass.Id));
            await AssertUniqueViolationAsync(other, "pk_prerender_unit");
        }

        await context.PrerenderPasses.ExecuteDeleteAsync(Cancellation);
        Assert.Equal(0, await context.PrerenderUnits.CountAsync(Cancellation));
    }

    [Fact]
    public async Task StatesAndScopeSurviveARoundTrip()
    {
        await using (var writer = Database.CreateContext())
        {
            var pass = NewPass();
            pass.State = PrerenderPassState.Paused;
            pass.RunningSince = null;
            pass.ActiveSeconds = ActiveSeconds;
            pass.Scope = """{"locale":"fr-FR"}""";
            var unit = NewUnit("16.20.1", "fr-FR");
            unit.State = PrerenderUnitState.Failed;
            pass.Units.Add(unit);
            writer.PrerenderPasses.Add(pass);
            await writer.SaveChangesAsync(Cancellation);
        }

        await using var reader = Database.CreateContext();
        var stored = await reader.PrerenderPasses.Include(pass => pass.Units)
            .SingleAsync(Cancellation);

        Assert.Equal(PrerenderPassState.Paused, stored.State);
        Assert.True(stored.IsActive);
        Assert.Null(stored.RunningSince);
        Assert.Equal(ActiveSeconds, stored.ActiveSeconds);
        Assert.Contains("fr-FR", stored.Scope, StringComparison.Ordinal);
        Assert.Equal(PrerenderUnitState.Failed, stored.Units.Single().State);
    }

    [Theory]
    [InlineData("'UPPERCASE'")]
    [InlineData("'abc'")]
    public async Task BuildHashMustBeALowercaseSha256(string hash)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            Database.ExecuteAsync(
                $"INSERT INTO prerender_build (hash) VALUES ({hash})",
                Cancellation));

        Assert.Equal("ck_prerender_build_hash", error.ConstraintName);
    }

    [Fact]
    public async Task BuildIsStoredOnce()
    {
        await using var first = Database.CreateContext();
        first.PrerenderBuilds.Add(new PrerenderBuild { Hash = Hash });
        await first.SaveChangesAsync(Cancellation);

        await using var second = Database.CreateContext();
        second.PrerenderBuilds.Add(new PrerenderBuild { Hash = Hash });

        await AssertUniqueViolationAsync(second, "pk_prerender_build");
    }

    private static async Task AssertUniqueViolationAsync(DbContext context, string constraint)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(Cancellation));

        var cause = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, cause.SqlState);
        Assert.Equal(constraint, cause.ConstraintName);
    }

    private static PrerenderPass NewPass() => new()
    {
        Epoch = 1,
        State = PrerenderPassState.Running,
        RunningSince = DateTimeOffset.UnixEpoch,
        AuthorId = 7,
    };

    private static PrerenderUnit NewUnit(string version, string locale, long passId = 0) => new()
    {
        PassId = passId,
        Version = version,
        Locale = locale,
        State = PrerenderUnitState.Pending,
    };

    private Task InsertPassAsync(string values) =>
        Database.ExecuteAsync(
            $"""
            INSERT INTO prerender_pass (epoch, state, scope, finished_at, running_since)
            VALUES ({values})
            """,
            Cancellation);
}
