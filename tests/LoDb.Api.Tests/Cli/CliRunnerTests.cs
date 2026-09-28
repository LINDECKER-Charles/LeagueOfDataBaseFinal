using System.Reflection;
using LoDb.Api.Cli;
using LoDb.Api.Tests.Cli.Fake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// Commands are found by the folder convention, chosen from the first arguments before any
/// web host exists, and run in a command host with the services Program.cs registers.
/// </summary>
public sealed class CliRunnerTests
{
    private static readonly Assembly TestAssembly = typeof(FakeRunCommand).Assembly;
    private static readonly string[] FakeCommandNames = ["fake fail", "fake run"];
    private static readonly string[] FlagArguments = ["--flag", "value"];
    private static readonly Action<IServiceCollection, IConfiguration> NoServices =
        static (_, _) => { };

    [Fact]
    public void ConventionFindsTheCommandsOfTheCliFolder()
    {
        var catalog = CliCommandCatalog.Discover(TestAssembly);

        Assert.Equal(FakeCommandNames, catalog.Commands.Select(static command => command.Name));
        Assert.All(catalog.Commands, static command => Assert.True(command.RequiresHost));
    }

    [Fact]
    public void ApiDeclaresHealthcheckAsACommandWithoutHost()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var healthcheck = Assert.Single(catalog.Commands, static c => c.Name == "healthcheck");
        Assert.False(healthcheck.RequiresHost);
        Assert.Equal(
            catalog.Commands.Count,
            catalog.Commands.Select(static c => c.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(new[] { "fake", "run" }, "fake run", new string[0])]
    [InlineData(
        new[] { "fake", "run", "--flag", "value" }, "fake run", new[] { "--flag", "value" })]
    [InlineData(new[] { "fake" }, null, new string[0])]
    [InlineData(new[] { "Fake", "run" }, null, new string[0])]
    public void MatchPicksTheCommandNamedByTheFirstArguments(
        string[] args,
        string? command,
        string[] arguments)
    {
        var invocation = CliCommandCatalog.Discover(TestAssembly).Match(args);

        Assert.Equal(command, invocation?.Command.Name);
        Assert.Equal(arguments, invocation?.Arguments ?? []);
    }

    [Theory]
    [InlineData(new[] { "healthcheck" }, true)]
    [InlineData(new[] { "catalog", "export" }, true)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "" }, false)]
    [InlineData(new[] { "--urls=http://localhost:5000" }, false)]
    [InlineData(new[] { "--environment=Development" }, false)]
    [InlineData(new[] { "-e" }, false)]
    [InlineData(new[] { "/key=value" }, false)]
    [InlineData(new[] { "LoDb:Workers:Enabled=false" }, false)]
    public void OnlyABareFirstWordIsACommand(string[] args, bool isCommand) =>
        Assert.Equal(isCommand, CliRunner.IsCommand(args));

    [Fact]
    public async Task CommandRunsInAHostHoldingTheRegisteredServices()
    {
        var probe = new FakeCommandProbe();

        var exitCode = await CliRunner.RunAsync(
            ["fake", "run", .. FlagArguments],
            TestAssembly,
            (services, _) => services.AddSingleton(probe));

        Assert.Equal(FakeCommandProbe.ExitCode, exitCode);
        Assert.Equal(FlagArguments, probe.Arguments);
    }

    [Fact]
    public async Task FailingCommandEndsWithTheFailureCode()
    {
        var exitCode = await CliRunner.RunAsync(["fake", "fail"], TestAssembly, NoServices);

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    [Fact]
    public async Task UnknownCommandEndsWithTheUsageCode()
    {
        var exitCode = await CliRunner.RunAsync(["fake", "jump"], TestAssembly, NoServices);

        Assert.Equal(CliExitCodes.Usage, exitCode);
    }
}
