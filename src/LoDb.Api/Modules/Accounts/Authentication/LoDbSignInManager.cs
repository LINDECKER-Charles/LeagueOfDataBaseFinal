using System.Security.Claims;
using LoDb.Api.Modules.Accounts.Tokens;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// Identity's sign-in manager, which also refuses banned accounts: at the password check,
/// and at each revalidation of a session cookie or of a refresh token. A sign-in to the
/// tokens of an app opens the family of its refresh tokens.
/// </summary>
/// <remarks>
/// The ban is checked after the password, not in <c>CanSignInAsync</c>, which runs before
/// it: whoever does not know the password must not learn that the account is banned.
/// </remarks>
internal sealed class LoDbSignInManager(
    UserManager<User> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<User> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<User>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<User> confirmation,
    RefreshFamilies families,
    SecondFactorWaiver waiver)
    : SignInManager<User>(
        userManager,
        contextAccessor,
        claimsFactory,
        optionsAccessor,
        logger,
        schemes,
        confirmation)
{
    public override async Task<SignInResult> CheckPasswordSignInAsync(
        User user,
        string password,
        bool lockoutOnFailure)
    {
        var result = await base.CheckPasswordSignInAsync(user, password, lockoutOnFailure);
        return result.Succeeded && user.IsBanned ? SignInResult.NotAllowed : result;
    }

    /// <remarks>
    /// Both the cookie revalidation and the refresh endpoint come here, so a ban closes the
    /// sessions and the tokens already handed out.
    /// </remarks>
    public override async Task<bool> ValidateSecurityStampAsync(
        User? user,
        string? securityStamp) =>
        user is { IsBanned: false } && await base.ValidateSecurityStampAsync(user, securityStamp);

    /// <remarks>
    /// Every sign-in ends here, with a password, a second factor or Google: one to the
    /// tokens names the new family in the principal the bearer handler writes into them,
    /// and the Development waiver of the second factor marks them all.
    /// </remarks>
    public override async Task SignInWithClaimsAsync(
        User user,
        AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims)
    {
        ArgumentNullException.ThrowIfNull(user);
        additionalClaims = waiver.Complete(additionalClaims);
        if (AuthenticationScheme != IdentityConstants.BearerScheme)
        {
            await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
            return;
        }

        var link = await families.OpenAsync(user.Id, Context.RequestAborted);
        await base.SignInWithClaimsAsync(
            user,
            authenticationProperties,
            [.. additionalClaims, .. RefreshFamilyClaims.Of(link)]);
    }
}
