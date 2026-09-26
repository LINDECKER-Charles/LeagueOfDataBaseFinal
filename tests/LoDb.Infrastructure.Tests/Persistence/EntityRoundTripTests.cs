using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.Builds;
using LoDb.Infrastructure.Persistence.Contact;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Infrastructure.Persistence.Scheduling;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Tests.Persistence;

/// <summary>
/// Every entity comes back as it was saved, every column filled, and the columns shared with
/// the legacy stack hold what it writes.
/// </summary>
public sealed class EntityRoundTripTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    // Whole seconds and UTC: the legacy columns keep no fraction, timestamptz takes UTC only.
    private static readonly DateTimeOffset At = new(2026, 9, 26, 8, 30, 15, TimeSpan.Zero);

    [Fact]
    public async Task UserRoundTrips()
    {
        var user = NewUser("round");
        user.Roles = ["ROLE_ADMIN", "ROLE_USER"];
        user.PasswordHash = "$2y$13$abcdefghijklmnopqrstuuv8Ma1b6Qx9O2Hqf9p1nq5e1V6h3pF4S";
        user.IsPublicProfile = true;
        user.FavoriteChampionId = "Ahri";
        user.FavoriteItemId = "3089";
        user.FavoriteRuneId = "8112";
        user.FavoriteSummonerId = "SummonerFlash";
        user.GoogleId = "109876543210987654321";
        user.RiotTagline = "EUW";
        user.IsSupporter = true;
        user.IsBanned = true;
        user.BannedAt = At.AddDays(1);
        user.BanReason = "Spam";
        user.EmailConfirmed = true;
        user.FavoriteSkinId = "Ahri_1";
        user.PreferredVersion = "16.19.1";
        user.SecurityStamp = "3SQKM6YQJ3BFMUKZHQ2WXDZB5JWRJ6XK";
        user.LockoutEnd = At.AddMinutes(15).AddTicks(1_234_560);
        user.LockoutEnabled = false;
        user.AccessFailedCount = 3;
        user.TwoFactorEnabled = true;

        await AssertRoundTripsAsync(user);
        Assert.Equal(
            ["[\"ROLE_ADMIN\",\"ROLE_USER\"]"],
            await Database.QueryAsync("SELECT roles::text FROM users", Cancellation));
    }

    [Fact]
    public async Task UserWithoutOptionalValuesRoundTrips()
    {
        var user = NewUser("minimal");

        await AssertRoundTripsAsync(user);
        Assert.Equal(
            ["[]"],
            await Database.QueryAsync("SELECT roles::text FROM users", Cancellation));
    }

    [Fact]
    public async Task BuildRoundTrips()
    {
        var owner = await SeedUserAsync();

        await AssertRoundTripsAsync(NewBuild(owner, "0123456789abcdef01234567"));
    }

    [Fact]
    public async Task BuildVoteRoundTrips()
    {
        var voter = await SeedUserAsync();
        var build = await SeedAsync(NewBuild(voter, "fedcba9876543210fedcba98"));

        await AssertRoundTripsAsync(new BuildVote
        {
            Value = -1,
            CreatedAt = At,
            BuildId = build.Id,
            VoterId = voter,
        });
    }

    [Fact]
    public async Task DonationRoundTrips()
    {
        var donor = await SeedUserAsync();

        await AssertRoundTripsAsync(new Donation
        {
            StripeSessionId = "cs_test_a1B2c3D4e5F6",
            AmountCents = 500,
            Currency = "eur",
            CreatedAt = At,
            UserId = donor,
        });
    }

    [Fact]
    public async Task ApiKeyRoundTrips()
    {
        var owner = await SeedUserAsync();

        await AssertRoundTripsAsync(NewApiKey(owner));
    }

    [Fact]
    public async Task ApiUsageRoundTrips()
    {
        var key = await SeedAsync(NewApiKey(await SeedUserAsync()));

        await AssertRoundTripsAsync(new ApiUsage
        {
            Day = new DateOnly(2026, 9, 26),
            Requests = 12_345_678_901,
            ApiKeyId = key.Id,
        });
    }

    [Fact]
    public async Task ContactMessageRoundTrips()
    {
        var sender = await SeedUserAsync();

        await AssertRoundTripsAsync(new ContactMessage
        {
            Category = "bug",
            Name = "Sender",
            Email = "sender@example.test",
            Subject = "Broken link",
            Message = "The link of the patch notes is broken.",
            Locale = "fr",
            Ip = "203.0.113.7",
            Status = "handled",
            CreatedAt = At,
            HandledAt = At.AddHours(3),
            UserId = sender,
        });
    }

    [Fact]
    public async Task DdragonAssetRoundTrips()
    {
        await AssertRoundTripsAsync(new DdragonAsset
        {
            Version = "16.19.1",
            Type = "champion",
            Key = "Ahri",
            Status = DdragonAssetStatus.Present,
            Sha256 = new string('a', 64),
            Extension = "png",
            RecordedAt = At.AddTicks(1_234_560),
        });
        Assert.Equal(
            ["present"],
            await Database.QueryAsync("SELECT status FROM ddragon_asset", Cancellation));
    }

    [Fact]
    public async Task DdragonVersionRoundTrips()
    {
        await AssertRoundTripsAsync(new DdragonVersion
        {
            Version = "16.19.1",
            Status = DdragonVersionStatus.Ready,
            Attempts = 2,
            NextAttemptAt = At.AddMinutes(5),
            DiscoveredAt = At,
            UpdatedAt = At.AddMinutes(20),
            ReadyAt = At.AddMinutes(20),
            PromotedAt = At.AddMinutes(21),
        });
        Assert.Equal(
            ["ready"],
            await Database.QueryAsync("SELECT status FROM ddragon_version", Cancellation));
    }

    [Fact]
    public async Task PeriodicJobStateRoundTrips() =>
        await AssertRoundTripsAsync(new PeriodicJobState
        {
            Name = "ddragon-watch",
            LastStartedAt = At,
            LastSucceededAt = At.AddSeconds(42),
        });

    private static User NewUser(string name) => new()
    {
        Email = $"{name}@example.test",
        UserName = name,
        Roles = [],
        CreatedAt = At,
    };

    private static Build NewBuild(int owner, string shareToken) => new()
    {
        Name = "Mid Ahri",
        ChampionId = "Ahri",
        GameVersion = "16.19.1",
        Description = "Burst first.",
        // Already in the canonical jsonb form, which PostgreSQL gives back.
        Runes = """{"primary": 8100, "secondary": 8200}""",
        Steps = """[{"items": ["3089", "3020"], "label": "core"}]""",
        IsPublic = true,
        ShareToken = shareToken,
        CreatedAt = At,
        UpdatedAt = At.AddMinutes(10),
        OwnerId = owner,
        GameMode = "aram",
        Language = "fr_FR",
    };

    private static ApiKey NewApiKey(int owner) => new()
    {
        Name = "Bot",
        KeyHash = new string('b', 64),
        KeyPrefix = "lodb_ab12",
        Plan = "monthly_plus",
        MonthlyQuota = 100_000,
        CreditsBalance = 5_000_000_000,
        RateLimitPerMin = 120,
        IsActive = false,
        CreatedAt = At,
        RevokedAt = At.AddDays(30),
        StripeCustomerId = "cus_T3stCustomer",
        StripeSubscriptionId = "sub_T3stSubscription",
        UserId = owner,
    };

    private async Task<int> SeedUserAsync() => (await SeedAsync(NewUser("owner"))).Id;

    // In a context of its own, so that the entity saved next keeps a null navigation.
    private async Task<T> SeedAsync<T>(T entity)
        where T : class
    {
        await using var context = Database.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(Cancellation);
        return entity;
    }

    private async Task AssertRoundTripsAsync<T>(T expected)
        where T : class
    {
        await SeedAsync(expected);

        await using var context = Database.CreateContext();
        var actual = await context.Set<T>().AsNoTracking().SingleAsync(Cancellation);
        Assert.Equivalent(expected, actual, strict: true);
    }
}
