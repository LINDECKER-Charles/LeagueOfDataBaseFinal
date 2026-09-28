using LoDb.Api.Cli;

namespace LoDb.Api.Tests.Cli.Fake;

/// <summary>
/// A command that throws, to check that the runner turns the failure into an exit code.
/// </summary>
internal sealed class FakeFailCommand : ICliCommand
{
    public static string Name => "fake fail";

    public Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The fake command fails on purpose.");
}
