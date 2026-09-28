using LoDb.Api.Workers;
using LoDb.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// The shared test host: fake clock on demand, background services off, and a storage root
/// that disappears with it.
/// </summary>
public sealed class ApiFactoryTests
{
    [Fact]
    public async Task ClockReplacesTheSystemTimeProvider()
    {
        var clock = new FakeTimeProvider();
        await using var factory = new ApiFactory { Clock = clock };

        Assert.Same(clock, factory.Services.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task BackgroundServicesAreSwitchedOff()
    {
        await using var factory = new ApiFactory();

        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal(bool.FalseString, configuration[ConventionalWorkers.EnabledKey]);
    }

    [Fact]
    public async Task StorageRootIsRemovedWithTheFactory()
    {
        string root;
        await using (var factory = new ApiFactory())
        {
            root = factory.StorageRoot;
            Assert.True(Directory.Exists(root));
        }

        Assert.False(Directory.Exists(root));
    }
}
