using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Profiles.Identity;

/// <summary>
/// <c>PUT /api/profile/identity</c>: renames the account and sets its Riot tag line, both
/// checked before anything changes.
/// </summary>
/// <remarks>
/// The security stamp stays: a new name revokes no session. A session keeps its old name in
/// its claims until its next revalidation; <c>/api/account/me</c> reads the stored one.
/// </remarks>
internal sealed class IdentityEndpoint(
    ProfileOwners owners,
    UserManager<User> users,
    ProfileAudit audit)
{
    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapPut(
                "/identity",
                static (
                    [FromBody] IdentityRequest request,
                    [FromServices] IdentityEndpoint endpoint,
                    HttpContext context) => endpoint.SetAsync(request, context))
            .WithName("setProfileIdentity")
            .WithSummary("Renames the account and sets its Riot tag line.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    public async Task<Results<NoContent, AccountProblem>> SetAsync(
        IdentityRequest request,
        HttpContext context)
    {
        if (await owners.FindAsync(context.User) is not { } owner)
        {
            return ProfileProblems.SignedOut();
        }

        var username = RegistrationRules.Username(request.Username);
        var tagline = IdentityRules.Tagline(request.RiotTagline);
        var errors = IdentityRules.Check(username, tagline);
        if (errors.IsEmpty && await IsTakenAsync(username, owner))
        {
            errors.Add(ProfileFields.Username, RegistrationRules.UsernameTaken);
        }

        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        owner.UserName = username;
        owner.RiotTagline = tagline;
        if (await UpdateAsync(owner) is { } refused)
        {
            return refused;
        }

        await audit.UpdatedAsync(ProfileAudit.IdentitySection, context.RequestAborted);
        return TypedResults.NoContent();
    }

    // Whatever the case: the unique index of the usernames compares lowercase.
    private async Task<bool> IsTakenAsync(string username, User owner) =>
        await users.FindByNameAsync(username) is { } holder && holder.Id != owner.Id;

    // The checks were made beforehand: only a name taken since can still refuse it.
    private async Task<AccountProblem?> UpdateAsync(User owner)
    {
        try
        {
            var updated = await users.UpdateAsync(owner);
            return updated.Succeeded ? null : Refused(updated);
        }
        catch (DbUpdateException exception)
            when (UniqueViolations.FieldOf(exception) == AccountFields.Username)
        {
            return Taken();
        }
    }

    private static AccountProblem Refused(IdentityResult result)
    {
        var isTaken = result.Errors.Any(static error =>
            error.Code == nameof(IdentityErrorDescriber.DuplicateUserName));
        if (isTaken)
        {
            return Taken();
        }

        var errors = new FieldErrors();
        errors.Add(ProfileFields.Username, result.Errors);
        return errors.ToProblem();
    }

    private static AccountProblem Taken()
    {
        var errors = new FieldErrors();
        errors.Add(ProfileFields.Username, RegistrationRules.UsernameTaken);
        return errors.ToProblem();
    }
}
