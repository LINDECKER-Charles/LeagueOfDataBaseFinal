namespace LoDb.Api.Tests.Cli.Fake;

/// <summary>
/// What the fake commands saw, registered by the test in the command host.
/// </summary>
internal sealed class FakeCommandProbe
{
    public const int ExitCode = 7;

    public IReadOnlyList<string>? Arguments { get; set; }
}
