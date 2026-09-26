using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Identity;

/// <summary>
/// <c>PUT /api/profile/password</c>: sets a first password on an account that has none, one
/// created through Google, which may then also sign in with it.
/// </summary>
/// <remarks>
/// Changing a password would need the current one: an account that has one gets a 409. The
/// security stamp stays, since a password added revokes nothing: the Google sessions and
/// tokens of the account remain its own.
/// </remarks>
internal sealed class SetPasswordEndpoint(
    ProfileOwners owners,
    UserManager<User> users,
    LoDbDbContext db,
    ProfileAudit audit)
{
    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapPut(
                "/password",
                static (
                    [FromBody] SetPasswordRequest request,
                    [FromServices] SetPasswordEndpoint endpoint,
                    HttpContext context) => endpoint.SetAsync(request, context))
            .WithName("setFirstPassword")
            .WithSummary("Sets a password on an account that has none.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

    public async Task<Results<NoContent, AccountProblem>> SetAsync(
        SetPasswordRequest request,
        HttpContext context)
    {
        if (await owners.FindAsync(context.User) is not { } owner)
        {
            return ProfileProblems.SignedOut();
        }

        if (owner.PasswordHash is not null)
        {
            return ProfileProblems.PasswordExists();
        }

        var errors = new FieldErrors();
        RegistrationRules.CheckPassword(errors, request.Password);
        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        owner.PasswordHash = users.PasswordHasher.HashPassword(owner, request.Password!);
        await db.SaveChangesAsync(context.RequestAborted);
        await audit.UpdatedAsync(ProfileAudit.PasswordSection, context.RequestAborted);
        return TypedResults.NoContent();
    }
}
