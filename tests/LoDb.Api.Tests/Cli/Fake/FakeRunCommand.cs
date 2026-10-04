using LoDb.Api.Cli;

namespace LoDb.Api.Tests.Cli.Fake;

/// <summary>
/// A command found by the convention under this assembly's <c>Cli</c> folder. It resolves a
/// service, so it only runs if the host holds the registrations the test passed.
/// </summary>
internal sealed class FakeRunCommand(FakeCommandProbe probe) : ICliCommand
{
    public static string Name => "fake run";

    public Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        probe.Arguments = arguments;
        return Task.FromResult(FakeCommandProbe.ExitCode);
    }
}
