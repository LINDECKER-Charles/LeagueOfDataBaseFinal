using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Catalog.WarmUp.Limits;

/// <summary>
/// Refuses at startup a loader that could open no stream, or keep one open for too long.
/// </summary>
internal sealed class WarmUpOptionsValidator : IValidateOptions<WarmUpOptions>
{
    private const int MaxStreamsLimit = 1024;
    private const int MaxPerHourLimit = 100_000;

    private static readonly TimeSpan MinLifetime = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(1);

    public ValidateOptionsResult Validate(string? name, WarmUpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> failures = [.. Failures(options)];
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> Failures(WarmUpOptions options)
    {
        if (options.MaxStreams is < 1 or > MaxStreamsLimit)
        {
            yield return "MaxStreams must lie between 1 and 1024.";
        }

        if (options.MaxStreamsPerClient < 1 || options.MaxStreamsPerClient > options.MaxStreams)
        {
            yield return "MaxStreamsPerClient must lie between 1 and MaxStreams.";
        }

        if (options.StreamLifetime < MinLifetime || options.StreamLifetime > MaxLifetime)
        {
            yield return "StreamLifetime must lie between 1 second and one hour.";
        }

        if (options.PerClientPerHour is < 1 or > MaxPerHourLimit)
        {
            yield return "PerClientPerHour must lie between 1 and 100000.";
        }
    }
}
