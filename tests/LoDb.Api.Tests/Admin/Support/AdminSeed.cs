using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.Builds;
using LoDb.Infrastructure.Persistence.Contact;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Support;

/// <summary>Rows the admin panels list, written in the database of the accounts host.</summary>
public static class AdminSeed
{
    private const string KeyPrefix = "lodb_";
    private const int KeySecretLength = 40;
    private const int DisplayedPrefixLength = 12;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>A public build of <paramref name="ownerId"/>, its champion named first.</summary>
    public static Task<int> BuildAsync(AccountsApp app, int ownerId, string name) =>
        AddAsync(app, new Build
        {
            Name = name,
            ChampionId = name.Split(' ')[0],
            GameVersion = "16.19.1",
            Runes = "{}",
            Steps = "[]",
            IsPublic = true,
            ShareToken = "share-" + name,
            GameMode = "sr",
            Language = "en_US",
            OwnerId = ownerId,
            CreatedAt = app.Clock.GetUtcNow(),
            UpdatedAt = app.Clock.GetUtcNow(),
        }, static build => build.Id);

    /// <summary>A contact message not handled yet; its id.</summary>
    public static Task<int> ContactAsync(AccountsApp app, string subject) =>
        AddAsync(app, new ContactMessage
        {
            Category = "bug",
            Name = "Sender",
            Email = "sender@example.test",
            Subject = subject,
            Message = "The link of the patch notes is broken.",
            Locale = "fr",
            Status = "new",
            CreatedAt = app.Clock.GetUtcNow(),
        }, static message => message.Id);

    /// <summary>A donation of <paramref name="amountCents"/>; its id.</summary>
    public static Task<int> DonationAsync(AccountsApp app, int? donorId, int amountCents) =>
        AddAsync(app, new Donation
        {
            StripeSessionId = "cs_test_" + Guid.NewGuid().ToString("N"),
            AmountCents = amountCents,
            Currency = "eur",
            CreatedAt = app.Clock.GetUtcNow(),
            UserId = donorId,
        }, static donation => donation.Id);

    /// <summary>
    /// An active key of the free plan, stored as the public API stores it; its id and its
    /// secret, to send in <c>X-Api-Key</c>.
    /// </summary>
    public static async Task<(int Id, string Secret)> ApiKeyAsync(AccountsApp app, int ownerId)
    {
        var secret = KeyPrefix + Convert.ToHexStringLower(
            RandomNumberGenerator.GetBytes(KeySecretLength / 2));
        var id = await AddAsync(app, new ApiKey
        {
            Name = "key-" + ownerId.ToString(CultureInfo.InvariantCulture),
            KeyHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(secret))),
            KeyPrefix = secret[..DisplayedPrefixLength],
            Plan = "free",
            MonthlyQuota = 500,
            RateLimitPerMin = 10,
            IsActive = true,
            CreatedAt = app.Clock.GetUtcNow(),
            UserId = ownerId,
        }, static key => key.Id);
        return (id, secret);
    }

    /// <summary>Runs <paramref name="action"/> with a context of its own scope.</summary>
    public static async Task<T> WithContextAsync<T>(
        AccountsApp app,
        Func<LoDbDbContext, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<LoDbDbContext>());
    }

    private static Task<int> AddAsync<T>(AccountsApp app, T row, Func<T, int> id)
        where T : class =>
        WithContextAsync(app, async context =>
        {
            context.Add(row);
            await context.SaveChangesAsync(Cancellation);
            return id(row);
        });
}
