using LoDb.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// A wrong network setting stops the host at start, with the name of the setting, instead
/// of serving on a port nobody expects.
/// </summary>
public sealed class HostingOptionsTests
{
    [Theory]
    [InlineData("LoDb:Hosting:MetricsPort", "8080", "must differ")]
    [InlineData("LoDb:Hosting:HttpPort", "0", "HttpPort")]
    [InlineData("LoDb:Hosting:KnownProxyNetworks:0", "not-a-network", "CIDR")]
    public async Task InvalidSettingStopsTheHostAtStart(string key, string value, string reason)
    {
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { [key] = value },
        };

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        var validation = FindValidationFailure(exception);
        Assert.NotNull(validation);
        Assert.Contains(reason, validation.Message, StringComparison.Ordinal);
    }

    private static OptionsValidationException? FindValidationFailure(Exception? exception) =>
        exception switch
        {
            null => null,
            OptionsValidationException validation => validation,
            AggregateException aggregate => aggregate.InnerExceptions
                .Select(FindValidationFailure)
                .FirstOrDefault(static found => found is not null),
            _ => FindValidationFailure(exception.InnerException),
        };
}
