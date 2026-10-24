using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Robots.Verification;
using LoDb.Infrastructure.Tests.Robots.Support;

namespace LoDb.Infrastructure.Tests.Robots.Verification;

/// <summary>
/// A robot is verified by a confirmed name under its operator's domains, unmasked by any
/// other answer, and left unresolved when the lookup failed; the network is shown once
/// confirmed only.
/// </summary>
public sealed class RobotVerifierTests
{
    private static readonly RobotProfile Googlebot =
        RobotCatalog.Identify(UserAgents.Googlebot)!;

    [Theory]
    [InlineData("crawl-66-249-66-1.googlebot.com", true, RobotVerdicts.Verified)]
    [InlineData("rate-limited-proxy-66-249-90-1.google.com", true, RobotVerdicts.Verified)]
    [InlineData("crawl-66-249-66-1.googlebot.com", false, RobotVerdicts.Mismatch)]
    [InlineData("googlebot.com.example.net", true, RobotVerdicts.Mismatch)]
    [InlineData("vps-1.example.net", true, RobotVerdicts.Mismatch)]
    public void TheNameDecides(string host, bool confirmed, string verdict) =>
        Assert.Equal(
            verdict,
            RobotVerifier.VerdictOf(
                Googlebot,
                new ReverseLookup { Host = host, IsConfirmed = confirmed }));

    [Fact]
    public void NoNameUnmasksAndAFailureOrADelayTellsNothing()
    {
        Assert.Equal(
            RobotVerdicts.Mismatch,
            RobotVerifier.VerdictOf(Googlebot, ReverseLookup.NoName));
        Assert.Equal(
            RobotVerdicts.Unresolved,
            RobotVerifier.VerdictOf(Googlebot, ReverseLookup.Failed));
        Assert.Equal(RobotVerdicts.Unresolved, RobotVerifier.VerdictOf(Googlebot, null));
    }

    [Fact]
    public void ARobotWithoutDomainsIsUnverifiable() =>
        Assert.Equal(
            RobotVerdicts.Unverifiable,
            RobotVerifier.VerdictOf(RobotCatalog.Identify(UserAgents.GptBot)!, null));

    [Fact]
    public void OnlyAConfirmedNameGivesANetwork()
    {
        var confirmed = new ReverseLookup
        {
            Host = "ec2-1-2-3-4.compute-1.amazonaws.com",
            IsConfirmed = true,
        };

        Assert.Equal("amazonaws.com", RobotVerifier.NetworkOf(confirmed));
        Assert.Null(RobotVerifier.NetworkOf(confirmed with { IsConfirmed = false }));
        Assert.Null(RobotVerifier.NetworkOf(ReverseLookup.NoName));
    }

    [Theory]
    [InlineData("crawl-66-249-66-1.googlebot.com.", "googlebot.com")]
    [InlineData("host.example.co.uk", "example.co.uk")]
    [InlineData("a.b.example.fr", "example.fr")]
    [InlineData("localhost", null)]
    public void TheRegistrableDomainKeepsWhoRunsTheName(string host, string? domain) =>
        Assert.Equal(domain, RegistrableDomain.Of(host));
}
