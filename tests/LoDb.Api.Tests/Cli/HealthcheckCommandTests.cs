using System.Net;
using System.Net.Sockets;
using LoDb.Api.Cli;
using LoDb.Api.Cli.Health;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// The Docker probe: exit code 0 when the local API answers 200 on <c>/healthz</c>, 1 for any
/// other answer or no answer, and nothing built beyond an HTTP client.
/// </summary>
public sealed class HealthcheckCommandTests
{
    [Theory]
    [InlineData(StatusCodes.Status200OK, CliExitCodes.Success)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, CliExitCodes.Failure)]
    [InlineData(StatusCodes.Status500InternalServerError, CliExitCodes.Failure)]
    public async Task ExitCodeFollowsTheLivenessAnswer(int status, int expected)
    {
        await using var server = await StubHealthServer.StartAsync(
            status,
            TestContext.Current.CancellationToken);

        var exitCode = await new HealthcheckCommand().ExecuteAsync(
            [PortArgument(server.Port)],
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, exitCode);
    }

    [Fact]
    public async Task ClosedPortFails()
    {
        var exitCode = await new HealthcheckCommand().ExecuteAsync(
            [PortArgument(ClosedPort())],
            TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    [Theory]
    [InlineData("not-a-port")]
    [InlineData("0")]
    [InlineData("70000")]
    public async Task InvalidPortFails(string port)
    {
        var exitCode = await new HealthcheckCommand().ExecuteAsync(
            [$"--LoDb:Hosting:HttpPort={port}"],
            TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCodes.Failure, exitCode);
    }

    [Fact]
    public async Task RunnerStartsTheProbeWithoutBuildingAnyHost()
    {
        await using var server = await StubHealthServer.StartAsync(
            StatusCodes.Status200OK,
            TestContext.Current.CancellationToken);

        // Registering services would throw: the probe must never get that far.
        var exitCode = await CliRunner.RunAsync(
            ["healthcheck", PortArgument(server.Port)],
            typeof(Program).Assembly,
            static (_, _) => throw new InvalidOperationException("No host may be built."));

        Assert.Equal(CliExitCodes.Success, exitCode);
    }

    private static string PortArgument(int port) => $"--LoDb:Hosting:HttpPort={port}";

    // A port that was free a moment ago and has nothing listening on it any more.
    private static int ClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
