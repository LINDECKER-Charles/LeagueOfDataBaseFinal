using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Tokens;

/// <summary>
/// The families of refresh tokens (ADR 0009, amendment of 2026-10-11): a refresh token serves
/// once, and the one a refresh replaced, played again, revokes its whole family.
/// </summary>
/// <remarks>
/// <para>
/// A thief and the app then hold tokens of the same family: whichever refreshes second plays
/// a spent token, and both lose the session. A sign-out revokes the family of the app that
/// signs out; signing out everywhere revokes every family of the account.
/// </para>
/// <para>
/// Each change is one conditional statement, so that two refreshes of one token at once
/// rotate the family once. The rows bypass the change tracker of the request's context,
/// whose account a sign-in is saving meanwhile.
/// </para>
/// </remarks>
internal sealed class RefreshFamilies(
    LoDbDbContext context,
    TimeProvider clock,
    IOptions<RefreshTokenOptions> options)
{
    // Past its expiry, a row only waits for the tokens the grace may have issued last.
    private static readonly TimeSpan PurgeDelay = TimeSpan.FromDays(1);

    /// <summary>Opens the family of a sign-in to the tokens, at its first generation.</summary>
    public async Task<RefreshLink> OpenAsync(int userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var link = new RefreshLink(Guid.CreateVersion7(now), Generation: 0);
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO refresh_token_families
                (id, user_id, generation, created_at, rotated_at, expires_at)
            VALUES ({link.FamilyId}, {userId}, 0, {now}, {now}, {ExpiryFrom(now)})
            """,
            cancellationToken);
        return link;
    }

    /// <summary>
    /// Gives the token of a sign-in made before the families the family named after it, at
    /// its first generation, unless a first refresh of it gave it one already.
    /// </summary>
    public Task AdoptAsync(
        int userId,
        RefreshLink legacy,
        DateTimeOffset expires,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        var now = clock.GetUtcNow();
        return context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO refresh_token_families
                (id, user_id, generation, created_at, rotated_at, expires_at)
            VALUES ({legacy.FamilyId}, {userId}, 0, {now}, {now}, {expires})
            ON CONFLICT (id) DO NOTHING
            """,
            cancellationToken);
    }

    /// <summary>Trades the token of <paramref name="presented"/> for the next generation.</summary>
    public async Task<RefreshRotation> RotateAsync(
        int userId,
        RefreshLink presented,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presented);
        var now = clock.GetUtcNow();
        var expires = ExpiryFrom(now);
        var rotated = await Family(userId, presented.FamilyId)
            .Where(family => family.RevokedAt == null && family.Generation == presented.Generation)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(family => family.Generation, family => family.Generation + 1)
                    .SetProperty(family => family.RotatedAt, now)
                    .SetProperty(family => family.ExpiresAt, expires),
                cancellationToken);
        return rotated == 1
            ? new RefreshRotation(presented.Next, IsReplay: false)
            : await AfterMissAsync(userId, presented, now, cancellationToken);
    }

    /// <summary>Revokes one family, if it is still live.</summary>
    public Task<int> RevokeAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return context.RefreshTokenFamilies
            .Where(family => family.Id == familyId && family.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(f => f.RevokedAt, now), cancellationToken);
    }

    /// <summary>Revokes every live family of the account.</summary>
    public Task<int> RevokeAllAsync(int userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return context.RefreshTokenFamilies
            .Where(family => family.UserId == userId && family.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(f => f.RevokedAt, now), cancellationToken);
    }

    /// <summary>Deletes the families whose every token ran out.</summary>
    public Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var before = clock.GetUtcNow() - PurgeDelay;
        return context.RefreshTokenFamilies
            .Where(family => family.ExpiresAt < before)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static DateTimeOffset ExpiryFrom(DateTimeOffset now) =>
        now + AuthenticationSetup.RefreshTokenLifetime;

    private IQueryable<RefreshTokenFamily> Family(int userId, Guid familyId) =>
        context.RefreshTokenFamilies.Where(
            family => family.Id == familyId && family.UserId == userId);

    // The token is not the current one: a retry of the last refresh within the grace gets
    // the same new generation; any other spent token revokes the family.
    private async Task<RefreshRotation> AfterMissAsync(
        int userId,
        RefreshLink presented,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var family = await Family(userId, presented.FamilyId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (family is null || family.RevokedAt is not null)
        {
            return RefreshRotation.Refused;
        }

        if (family.Generation == presented.Next.Generation
            && now - family.RotatedAt <= options.Value.ReuseGrace)
        {
            return new RefreshRotation(presented.Next, IsReplay: false);
        }

        await RevokeAsync(family.Id, cancellationToken);
        return RefreshRotation.Replayed;
    }
}
