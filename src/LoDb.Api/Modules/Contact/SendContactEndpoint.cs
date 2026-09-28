using System.Globalization;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Contact;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Contact;

/// <summary>
/// <c>POST /api/contact</c>: keeps a message of the footer form for the admin inbox and
/// forwards it to the team.
/// </summary>
/// <remarks>
/// Open to visitors, behind the forgery guard and the <c>contact</c> rate limit. The message
/// and its notification are written together: the outbox sends it later, so a relay outage
/// never fails the request. No recipient set, the message is only kept.
/// </remarks>
internal sealed class SendContactEndpoint(
    LoDbDbContext db,
    IEmailOutbox outbox,
    IOptions<ContactOptions> options,
    UserManager<User> users,
    TimeProvider clock)
{
    private const string NewStatus = "new";

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                ContactRoutes.Path,
                static (
                    [FromBody] ContactRequest request,
                    [FromServices] SendContactEndpoint endpoint,
                    HttpContext context) => endpoint.SendAsync(request, context))
            .WithTags(ContactRoutes.Tag)
            .RequireRateLimiting(RateLimitingPolicies.Contact)
            .WithName("sendContactMessage")
            .WithSummary("Sends a message to the team of the site.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    public async Task<Results<NoContent, AccountProblem>> SendAsync(
        ContactRequest request,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        // A robot is told what a person is, and nothing is kept or sent.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return TypedResults.NoContent();
        }

        var errors = new FieldErrors();
        if (ContactRules.Read(request, errors) is not { } submission)
        {
            return errors.ToProblem();
        }

        var message = Message(submission, context);
        db.ContactMessages.Add(message);
        if (ContactNotification.Recipient(options.Value) is not null)
        {
            await outbox.EnqueueAsync(
                ContactNotification.Of(message, options.Value),
                context.RequestAborted);
        }

        await db.SaveChangesAsync(context.RequestAborted);
        return TypedResults.NoContent();
    }

    private ContactMessage Message(ContactSubmission submission, HttpContext context) => new()
    {
        Category = submission.Category,
        Name = submission.Name,
        Email = submission.Email,
        Subject = submission.Subject,
        Message = submission.Message,
        Locale = submission.LocaleCode,
        Ip = Ip(context),
        Status = NewStatus,
        CreatedAt = clock.GetUtcNow(),
        UserId = SenderId(context),
    };

    // The signed-in sender, if any: the form is open to visitors.
    private int? SenderId(HttpContext context) =>
        int.TryParse(
            users.GetUserId(context.User),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var id)
            ? id
            : null;

    // Put there by the forwarded headers middleware; IPv4 written as IPv4, as the legacy did.
    private static string? Ip(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        return address is { IsIPv4MappedToIPv6: true }
            ? address.MapToIPv4().ToString()
            : address?.ToString();
    }
}
