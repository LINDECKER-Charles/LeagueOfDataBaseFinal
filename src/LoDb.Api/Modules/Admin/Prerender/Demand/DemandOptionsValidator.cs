using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand;

/// <summary>
/// Refuses at startup a queue that would read its pages from anything but an http URL on the
/// stack network, or whose share, retention, bound or tries make no sense. A missing URL is
/// not refused: nothing new then enters the queue.
/// </summary>
internal sealed class DemandOptionsValidator : IValidateOptions<DemandOptions>
{
    private const int MostPercent = 100;
    private const int MostRetentionDays = 365;
    private const int FewestPendingPages = 100;
    private const int MostPendingPages = 1_000_000;
    private const int MostAttempts = 10;

    public ValidateOptionsResult Validate(string? name, DemandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddIf(
            failures,
            !string.IsNullOrWhiteSpace(options.RefusedPagesUrl) && !IsUrl(options.RefusedPagesUrl),
            "RefusedPagesUrl must be an http or https URL, without a query.");
        AddIf(
            failures,
            options.QueueSharePercent is < 1 or > MostPercent,
            $"QueueSharePercent must be between 1 and {MostPercent}.");
        AddIf(
            failures,
            options.RetentionDays is < 1 or > MostRetentionDays,
            $"RetentionDays must be between 1 and {MostRetentionDays}.");
        AddIf(
            failures,
            options.MaxPendingPages is < FewestPendingPages or > MostPendingPages,
            $"MaxPendingPages must be between {FewestPendingPages} and {MostPendingPages}.");
        AddIf(
            failures,
            options.MaxAttempts is < 1 or > MostAttempts,
            $"MaxAttempts must be between 1 and {MostAttempts}.");
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
        && url.Query.Length == 0
        && url.UserInfo.Length == 0;

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
