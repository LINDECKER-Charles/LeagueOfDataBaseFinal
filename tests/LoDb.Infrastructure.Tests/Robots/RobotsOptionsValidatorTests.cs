using LoDb.Infrastructure.Robots;

namespace LoDb.Infrastructure.Tests.Robots;

/// <summary>The defaults start; a journal that could never write, or detect, does not.</summary>
public sealed class RobotsOptionsValidatorTests
{
    [Fact]
    public void TheDefaultsAreValid() =>
        Assert.True(new RobotsOptionsValidator().Validate(null, new RobotsOptions()).Succeeded);

    [Fact]
    public void BoundsOutOfReasonAreRefused()
    {
        var result = new RobotsOptionsValidator().Validate(null, new RobotsOptions
        {
            BatchSize = 0,
            UndeclaredThreshold = 0,
            UndeclaredMemory = TimeSpan.FromMinutes(1),
            DnsParallelism = 0,
        });

        Assert.True(result.Failed);
        Assert.Equal(4, result.Failures!.Count());
    }
}
