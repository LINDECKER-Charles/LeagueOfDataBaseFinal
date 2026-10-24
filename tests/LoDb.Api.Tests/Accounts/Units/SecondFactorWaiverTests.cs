using System.Security.Claims;
using LoDb.Api.Modules.Accounts;
using LoDb.Api.Modules.Accounts.Authentication;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The Development waiver of the second factor: it marks a password sign-in as made with
/// one, and the host refuses it in any other environment.
/// </summary>
public sealed class SecondFactorWaiverTests
{
    private static readonly Claim Password =
        new(AuthenticationMethods.ClaimType, AuthenticationMethods.Password);

    private static readonly Claim MultiFactor =
        new(AuthenticationMethods.ClaimType, AuthenticationMethods.MultiFactor);

    [Fact]
    public void TheWaiverMarksAPasswordSignInAsMultiFactor()
    {
        var claims = Waiver(isWaived: true).Complete([Password]);

        Assert.Equal(
            [AuthenticationMethods.Password, AuthenticationMethods.MultiFactor],
            claims.Select(static claim => claim.Value));
    }

    [Fact]
    public void TheWaiverAddsNoSecondMark()
    {
        var claims = Waiver(isWaived: true).Complete([Password, MultiFactor]);

        Assert.Equal(2, claims.Count);
    }

    [Fact]
    public void WithoutTheWaiverTheClaimsStayAsTheyAre()
    {
        var claims = Waiver(isWaived: false).Complete([Password]);

        Assert.Equal(Password, Assert.Single(claims));
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void OnlyDevelopmentAcceptsTheWaiver(string environment, bool isAccepted)
    {
        var validator = new AccountsOptionsValidator(
            new HostingEnvironment { EnvironmentName = environment });

        var result = validator.Validate(null, new AccountsOptions { WaiveSecondFactor = true });

        Assert.Equal(isAccepted, result.Succeeded);
    }

    private static SecondFactorWaiver Waiver(bool isWaived) =>
        new(Options.Create(new AccountsOptions { WaiveSecondFactor = isWaived }));
}
