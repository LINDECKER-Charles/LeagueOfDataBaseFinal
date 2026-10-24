using LoDb.Infrastructure.Robots;
using LoDb.Infrastructure.Robots.Detection;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Robots.Detection;

/// <summary>
/// A client becomes a robot at the request that reaches the threshold within the window,
/// stays one for the memory, and a reader's pace never gets there.
/// </summary>
public sealed class UndeclaredDetectorTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheThresholdWithinTheWindowFlagsTheClient()
    {
        var detector = Detector(threshold: 3);

        detector.Count("client", Start);
        detector.Count("client", Start.AddMinutes(1));
        var before = detector.IsRobot("client", Start.AddMinutes(1));
        detector.Count("client", Start.AddMinutes(2));

        Assert.False(before);
        Assert.True(detector.IsRobot("client", Start.AddMinutes(2)));
        Assert.True(detector.IsRobot("client", Start.AddHours(23)));
        Assert.False(detector.IsRobot("client", Start.AddHours(25)));
    }

    [Fact]
    public void ASlowerPaceStartsAWindowAgain()
    {
        var detector = Detector(threshold: 3);

        detector.Count("reader", Start);
        detector.Count("reader", Start.AddMinutes(9));
        detector.Count("reader", Start.AddMinutes(11));

        Assert.False(detector.IsRobot("reader", Start.AddMinutes(11)));
    }

    [Fact]
    public void ClientsAreToldApartAndForgottenBeyondTheBound()
    {
        var detector = Detector(threshold: 2, maxClients: 2);

        detector.Count("robot", Start);
        detector.Count("robot", Start);
        detector.Count("quiet", Start);
        detector.Count("new", Start.AddMinutes(30));

        Assert.True(detector.IsRobot("robot", Start.AddMinutes(30)));
        Assert.False(detector.IsRobot("quiet", Start.AddMinutes(30)));
    }

    private static UndeclaredDetector Detector(int threshold, int maxClients = 100) =>
        new(Options.Create(new RobotsOptions
        {
            UndeclaredThreshold = threshold,
            UndeclaredWindow = TimeSpan.FromMinutes(10),
            UndeclaredMemory = TimeSpan.FromHours(24),
            MaxTrackedClients = maxClients,
        }));
}
