using System.Security.Claims;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Api.Modules.Accounts.Session;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Accounts.Registration;

/// <summary>
/// <c>POST /api/account/register</c>: creates the account, queues its verification e-mail in
/// the same transaction, then signs it in, as the legacy stack does.
/// </summary>
internal sealed class RegisterEndpoint(
    UserManager<User> users,
    SignInManager<User> signIn,
    LoDbDbContext db,
    AccountMail mail,
    AccountAudit audit,
    SessionReader sessions,
    TimeProvider clock)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/register",
                static (
                    [FromBody] RegisterRequest request,
                    [FromServices] RegisterEndpoint endpoint,
                    HttpContext context) => endpoint.RegisterAsync(request, context))
            .RequireRateLimiting(RateLimitingPolicies.Registration)
            .WithName("registerAccount")
            .WithSummary("Creates an account, signs it in and sends its verification e-mail.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    public async Task<Results<Created<AccountSession>, AccountProblem>> RegisterAsync(
        RegisterRequest request,
        HttpContext context)
    {
        var errors = RegistrationRules.Check(request);
        if (errors.IsEmpty)
        {
            await CheckTakenAsync(request, errors);
        }

        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        var user = NewUser(request);
        if (await CreateAsync(user, request, context.RequestAborted) is { } refused)
        {
            return refused;
        }

        Claim[] methods = [new(AuthenticationMethods.ClaimType, AuthenticationMethods.Password)];
        await signIn.SignInWithClaimsAsync(user, isPersistent: false, methods);
        await audit.RecordAsync(AuditAction.UserRegister, user, context.RequestAborted);
        return TypedResults.Created(AccountRoutes.Me, await sessions.ReadAsync(context.User));
    }

    // Whatever the case: the unique indexes of the table compare lowercase.
    private async Task CheckTakenAsync(RegisterRequest request, FieldErrors errors)
    {
        if (await users.FindByEmailAsync(RegistrationRules.Email(request.Email)) is not null)
        {
            errors.Add(AccountFields.Email, RegistrationRules.EmailTaken);
        }

        if (await users.FindByNameAsync(RegistrationRules.Username(request.Username)) is not null)
        {
            errors.Add(AccountFields.Username, RegistrationRules.UsernameTaken);
        }
    }

    private User NewUser(RegisterRequest request) => new()
    {
        UserName = RegistrationRules.Username(request.Username),
        Email = RegistrationRules.Email(request.Email),
        Roles = [],
        CreatedAt = clock.GetUtcNow(),
    };

    // The account and its e-mail are written together, or neither is.
    private async Task<AccountProblem?> CreateAsync(
        User user,
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var created = await users.CreateAsync(user, request.Password!);
            if (!created.Succeeded)
            {
                return Refused(created);
            }

            var locale = request.Locale ?? UiLocales.Fallback;
            await mail.SendConfirmationAsync(user, locale, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (UniqueViolations.FieldOf(exception) is { } field)
        {
            db.Entry(user).State = EntityState.Detached;
            var errors = new FieldErrors();
            errors.Add(field, UniqueViolations.TakenCode(field));
            return errors.ToProblem();
        }
    }

    // The rules were checked beforehand: only a name taken since can still refuse it.
    private static AccountProblem Refused(IdentityResult result)
    {
        var errors = new FieldErrors();
        foreach (var error in result.Errors)
        {
            if (error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
            {
                errors.Add(AccountFields.Username, RegistrationRules.UsernameTaken);
            }
            else
            {
                errors.Add(AccountFields.Password, error.Code);
            }
        }

        return errors.ToProblem();
    }
}
