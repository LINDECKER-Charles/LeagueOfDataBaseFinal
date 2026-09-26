using Microsoft.Extensions.Time.Testing;

namespace LoDb.Desktop.Tests.Support;

/// <summary>What a test sets up around the host.</summary>
internal sealed record DesktopHostSetup
{
    public required Uri ApiOrigin { get; init; }

    public string? GoogleClientId { get; init; }

    /// <summary>A data folder kept across two hosts (restart); a fresh one when null.</summary>
    public string? DataDirectory { get; init; }

    public string? ShellDirectory { get; init; }

    public FakeShell Shell { get; init; } = new();

    public FakeBrowser Browser { get; init; } = new();

    public FakeTimeProvider Time { get; init; } =
        new(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
}
