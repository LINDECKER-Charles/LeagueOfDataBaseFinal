using LoDb.Api.Cli;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// The list of the API's commands that need a host is closed: a new one fails here until
/// <see cref="DevelopmentCommandHostTests"/> runs it.
/// </summary>
public sealed class HostedCommandsTests
{
    [Fact]
    public void EveryCommandWithAHostIsCovered()
    {
        var covered = new[]
        {
            "admin create", "admin mfa-invite", "admin mfa-reset", "admin root",
            "analytics import", "audit import",
            "baseline mark-applied", "catalog export", "client-policy publish", "ingest",
            "migrate",
        };

        var hosted = CliCommandCatalog.Discover(typeof(Program).Assembly).Commands
            .Where(static command => command.RequiresHost)
            .Select(static command => command.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(covered, hosted);
    }
}
