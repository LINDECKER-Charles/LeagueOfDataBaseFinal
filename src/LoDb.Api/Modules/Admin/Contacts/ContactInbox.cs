using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Contact;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>The messages of the contact form, newest first, new or handled.</summary>
internal sealed class ContactInbox(LoDbDbContext db, TimeProvider clock)
{
    private static readonly TimeSpan WeekWindow = TimeSpan.FromDays(7);

    public async Task<AdminContactPage> ListAsync(
        string? status,
        int page,
        CancellationToken cancellationToken)
    {
        var messages = db.ContactMessages.AsNoTracking();
        var matching = ContactStatuses.Parse(status) is { } wanted
            ? messages.Where(message => message.Status == wanted)
            : messages;
        var total = await matching.CountAsync(cancellationToken);
        var rows = await Rows(matching
                .OrderByDescending(static message => message.CreatedAt)
                .ThenByDescending(static message => message.Id)
                .Skip(AdminPaging.Skip(page))
                .Take(AdminPaging.PageSize))
            .ToListAsync(cancellationToken);
        return new AdminContactPage
        {
            Stats = await StatsAsync(messages, cancellationToken),
            Items = rows,
            Total = total,
            Page = page,
            Pages = AdminPaging.Pages(total),
        };
    }

    private static IQueryable<AdminContactRow> Rows(IQueryable<ContactMessage> messages) =>
        messages.Select(static message => new AdminContactRow
        {
            Id = message.Id,
            Category = message.Category,
            Name = message.Name,
            Email = message.Email,
            Subject = message.Subject,
            Message = message.Message,
            Locale = message.Locale,
            Status = message.Status,
            CreatedAt = message.CreatedAt,
            HandledAt = message.HandledAt,
            User = message.User == null
                ? null
                : new AdminUserRef
                {
                    Id = message.User.Id,
                    Username = message.User.UserName!,
                    IsBanned = message.User.IsBanned,
                },
        });

    private async Task<AdminContactStats> StatsAsync(
        IQueryable<ContactMessage> messages,
        CancellationToken cancellationToken)
    {
        var since = clock.GetUtcNow() - WeekWindow;
        return new AdminContactStats
        {
            Total = await messages.CountAsync(cancellationToken),
            New = await messages.CountAsync(
                static message => message.Status == ContactStatuses.New,
                cancellationToken),
            Handled = await messages.CountAsync(
                static message => message.Status == ContactStatuses.Handled,
                cancellationToken),
            Week = await messages.CountAsync(
                message => message.CreatedAt >= since,
                cancellationToken),
        };
    }
}
