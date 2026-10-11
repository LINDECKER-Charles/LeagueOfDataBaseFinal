using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Audience;

/// <summary>
/// Refuses at startup a pull that would reach anything but a bare https host, or bounds that
/// would make it unbounded. Missing settings are not refused: the pull then stays dormant.
/// </summary>
internal sealed class AudienceOptionsValidator : IValidateOptions<AudienceOptions>
{
    private const int MaxRefreshDays = 31;
    private const int MaxPageSize = 1_000;
    private const int MaxPages = 1_000;
    private const int MinResponseBytes = 1_024;
    private const int MaxClipWindowDays = 366;
    private const double MinClipFactor = 1;

    private static readonly TimeSpan MinPeriod = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(1);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(5);

    public ValidateOptionsResult Validate(string? name, AudienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddIf(
            failures,
            !string.IsNullOrWhiteSpace(options.Host)
            && Uri.CheckHostName(options.Host) != UriHostNameType.Dns,
            "Host must be a bare host name: no scheme, port nor path.");
        AddIf(
            failures,
            options.PullPeriod < MinPeriod || options.PullPeriod > MaxPeriod,
            "PullPeriod must lie in 1 minute – 1 day.");
        AddIf(
            failures,
            options.RefreshDays is < 1 or > MaxRefreshDays,
            $"RefreshDays must lie between 1 and {MaxRefreshDays}.");
        AddRequestFailures(failures, options);
        AddClipFailures(failures, options);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddRequestFailures(List<string> failures, AudienceOptions options)
    {
        AddIf(
            failures,
            options.PageSize is < 1 or > MaxPageSize,
            $"PageSize must lie between 1 and {MaxPageSize}.");
        AddIf(
            failures,
            options.MaxPages is < 1 or > MaxPages,
            $"MaxPages must lie between 1 and {MaxPages}.");
        AddIf(
            failures,
            options.RequestTimeout <= TimeSpan.Zero || options.RequestTimeout > MaxTimeout,
            "RequestTimeout must be positive and 5 minutes at most.");
        AddIf(
            failures,
            options.MaxResponseBytes < MinResponseBytes,
            $"MaxResponseBytes must be at least {MinResponseBytes}.");
    }

    private static void AddClipFailures(List<string> failures, AudienceOptions options)
    {
        AddIf(
            failures,
            !(options.ClipFactor >= MinClipFactor) || double.IsInfinity(options.ClipFactor),
            "ClipFactor must be a finite number, 1 or more.");
        AddIf(
            failures,
            options.ClipWindowDays is < 1 or > MaxClipWindowDays,
            $"ClipWindowDays must lie between 1 and {MaxClipWindowDays}.");
    }

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
