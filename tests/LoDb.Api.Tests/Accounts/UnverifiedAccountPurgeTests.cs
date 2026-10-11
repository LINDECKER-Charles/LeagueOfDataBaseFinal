using System.Globalization;
using LoDb.Api.Modules.Accounts.Verification;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;
using LoDb.Api.Workers;
using LoDb.Api.Workers.Accounts;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.Builds;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The hourly erasure of the accounts still unverified a week after their creation: erased
/// with what hangs on them and journaled; a verified or recent account, and one holding
/// something worth keeping, stays.
/// </summary>
public sealed class UnverifiedAccountPurgeTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void PurgeIsDiscoveredAsAWorker() =>
        Assert.Contains(
            typeof(UnverifiedAccountPurgeJob),
            ConventionalWorkers.Discover(typeof(Program).Assembly));

    [Fact]
    public async Task UnverifiedAccountIsErasedOnceAWeekOld()
    {
        var forgotten = await App.SeedAsync(Unverified("Oublie"));
        await App.SeedAsync(Named("Fidele"));
        App.Clock.Advance(Week - Minute);
        await App.SeedAsync(Unverified("Recent"));

        await RunAsync();
        var keptAtFirst = await NamesAsync();
        App.Clock.Advance(Hour);
        var outcome = await RunAsync();

        Assert.Equal(["Fidele", "Oublie", "Recent"], keptAtFirst);
        Assert.Equal(JobRunOutcome.Succeeded, outcome);
        Assert.Equal(["Fidele", "Recent"], await NamesAsync());
        var erasure = Assert.Single(
            await App.AuditAsync(),
            static entry => entry.Action == AuditAction.AccountDelete);
        Assert.Equal(forgotten.Id.ToString(CultureInfo.InvariantCulture), erasure.TargetId);
        Assert.Equal(("Oublie", AuditActorType.Anonymous), (erasure.Target, erasure.ActorType));
        Assert.Equal(UnverifiedAccountPurge.Reason, AccountsApp.Meta(erasure, "reason"));
    }

    [Fact]
    public async Task ErasureTakesTheVotesAndCountsOfTheAccount()
    {
        var author = await App.SeedAsync(Named("Auteur"));
        var forgotten = await App.SeedAsync(Unverified("Oublie"));
        var build = await InsertBuildAsync(author);
        await using (var db = App.Database())
        {
            db.BuildVotes.Add(new BuildVote
            {
                BuildId = build.Id,
                VoterId = forgotten.Id,
                Value = 1,
                CreatedAt = App.Clock.GetUtcNow(),
            });
            db.Set<IdentityUserToken<int>>().Add(new IdentityUserToken<int>
            {
                UserId = forgotten.Id,
                LoginProvider = "LoDb",
                Name = "confirm-email-requests",
                Value = "1;",
            });
            await db.SaveChangesAsync(Cancellation);
        }

        App.Clock.Advance(Week + Minute);
        await RunAsync();

        await using var after = App.Database();
        Assert.Empty(await after.BuildVotes.Where(vote => vote.BuildId == build.Id)
            .ToListAsync(Cancellation));
        Assert.Empty(await after.Set<IdentityUserToken<int>>()
            .Where(token => token.UserId == forgotten.Id)
            .ToListAsync(Cancellation));
        Assert.NotNull(await after.Builds.FindAsync([build.Id], Cancellation));
    }

    [Fact]
    public async Task AccountHoldingSomethingWorthKeepingStays()
    {
        await InsertBuildAsync(await App.SeedAsync(Unverified("Batisseur")));
        await InsertKeyAsync(await App.SeedAsync(Unverified("Developpeur")));
        await InsertDonationAsync(await App.SeedAsync(Unverified("Donateur")));
        await App.SeedAsync(Unverified("Banni") with { Banned = true });
        await App.SeedAsync(Unverified("Googleur") with { GoogleId = "google-1", Password = null });
        await App.SeedAsync(Unverified("Soutien"));
        await App.UsersAsync(async users =>
        {
            var supporter = await users.FindByNameAsync("Soutien");
            supporter!.IsSupporter = true;
            return await users.UpdateAsync(supporter);
        });
        await App.SeedAsync(Unverified("Gardien"));
        await GrantAdminAsync("Gardien");
        var before = await NamesAsync();

        App.Clock.Advance(Week + Minute);
        await RunAsync();

        Assert.Equal(7, before.Count);
        Assert.Equal(before, await NamesAsync());
        Assert.DoesNotContain(
            await App.AuditAsync(),
            static entry => entry.Action == AuditAction.AccountDelete);
    }

    [Fact]
    public async Task PurgeRunsOnceAnHour()
    {
        var job = ActivatorUtilities.CreateInstance<UnverifiedAccountPurgeJob>(App.Services);

        var first = await job.RunIfDueAsync(Cancellation);
        var early = await job.RunIfDueAsync(Cancellation);
        App.Clock.Advance(Hour);
        var next = await job.RunIfDueAsync(Cancellation);

        Assert.Equal(
            (JobRunOutcome.Succeeded, JobRunOutcome.NotDue, JobRunOutcome.Succeeded),
            (first, early, next));
    }

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(postgres, withGoogle: false);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static AccountSeed Named(string username) => new()
    {
        Username = username,
        Email = username.ToLowerInvariant() + "@example.test",
    };

    private static AccountSeed Unverified(string username) =>
        Named(username) with { EmailConfirmed = false };

    // A job of its own each run: its state row says when it last ran.
    private Task<JobRunOutcome> RunAsync() =>
        ActivatorUtilities.CreateInstance<UnverifiedAccountPurgeJob>(App.Services)
            .RunIfDueAsync(Cancellation);

    private async Task<List<string>> NamesAsync()
    {
        await using var db = App.Database();
        return await db.Users.AsNoTracking()
            .Select(static user => user.UserName!)
            .OrderBy(static name => name)
            .ToListAsync(Cancellation);
    }

    private async Task<Build> InsertBuildAsync(User owner)
    {
        await using var db = App.Database();
        var build = new StoredBuild().ToEntity(owner.Id);
        db.Builds.Add(build);
        await db.SaveChangesAsync(Cancellation);
        return build;
    }

    private async Task InsertKeyAsync(User owner)
    {
        await using var db = App.Database();
        db.ApiKeys.Add(new ApiKey
        {
            Name = "default",
            KeyHash = new string('a', 64),
            KeyPrefix = "lodb_0123456",
            Plan = "free",
            MonthlyQuota = 500,
            RateLimitPerMin = 10,
            IsActive = true,
            CreatedAt = App.Clock.GetUtcNow(),
            UserId = owner.Id,
        });
        await db.SaveChangesAsync(Cancellation);
    }

    private async Task InsertDonationAsync(User donor)
    {
        await using var db = App.Database();
        db.Donations.Add(new Donation
        {
            StripeSessionId = "cs_test_purge",
            AmountCents = 500,
            Currency = "eur",
            CreatedAt = App.Clock.GetUtcNow(),
            UserId = donor.Id,
        });
        await db.SaveChangesAsync(Cancellation);
    }

    private async Task GrantAdminAsync(string username)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var created = await roles.CreateAsync(new Role { Name = Role.Admin });
        var granted = await users.AddToRoleAsync(
            (await users.FindByNameAsync(username))!,
            Role.Admin);
        Assert.True(created.Succeeded && granted.Succeeded);
    }
}
