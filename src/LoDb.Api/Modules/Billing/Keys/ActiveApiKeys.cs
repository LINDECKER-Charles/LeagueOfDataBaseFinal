using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Keys;

/// <summary>The key a purchase lands on: the account's active one.</summary>
internal static class ActiveApiKeys
{
    /// <summary>
    /// The active key of the account <paramref name="userId"/>, the newest if a legacy fault
    /// left several; null when it has none.
    /// </summary>
    public static Task<ApiKey?> ActiveOfAsync(
        this IQueryable<ApiKey> keys,
        int userId,
        CancellationToken cancellationToken) =>
        keys.Where(key => key.UserId == userId && key.IsActive)
            .OrderByDescending(key => key.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
