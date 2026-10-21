using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.PageStore;

/// <summary>
/// Refuses at startup a store that would be reached over cleartext or through anything but a
/// bare base URL, a bucket or region S3 would reject, a class that is not One Zone. Missing
/// settings are not refused: the store then stays dormant, and so do its region and class (an
/// empty <c>LoDb__Prerender__Region=</c> in an env file must not stop the host).
/// </summary>
internal sealed partial class PageStoreOptionsValidator : IValidateOptions<PageStoreOptions>
{
    private const string RootPath = "/";

    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(5);

    // The development store (MinIO, plain http) knows no One Zone class: there alone may it
    // be stood in for, so a host never pays for STANDARD by mistake.
    private static readonly string[] DevelopmentClasses =
    [
        PageStoreOptions.ReducedRedundancy,
        StoredPage.StandardStorageClass,
    ];

    public ValidateOptionsResult Validate(string? name, PageStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddEndpointFailures(failures, options);
        AddIf(
            failures,
            !string.IsNullOrWhiteSpace(options.Bucket) && !BucketName().IsMatch(options.Bucket),
            "Bucket must be an S3 bucket name: 3 to 63 lower-case letters, digits, dots, hyphens.");
        if (options.IsConfigured)
        {
            AddRegionAndClassFailures(failures, options);
        }

        AddIf(
            failures,
            options.RequestTimeout <= TimeSpan.Zero || options.RequestTimeout > MaxTimeout,
            "RequestTimeout must be positive and 5 minutes at most.");
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddEndpointFailures(List<string> failures, PageStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return;
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.AbsolutePath != RootPath
            || endpoint.Query.Length > 0
            || endpoint.Fragment.Length > 0
            || endpoint.UserInfo.Length > 0)
        {
            failures.Add("Endpoint must be a base URL: a scheme and a host, no path nor query.");
            return;
        }

        var isAllowedScheme = endpoint.Scheme == Uri.UriSchemeHttps
            || (options.AllowInsecureEndpoint && endpoint.Scheme == Uri.UriSchemeHttp);
        AddIf(
            failures,
            !isAllowedScheme,
            "Endpoint must be https (http only with AllowInsecureEndpoint, in development).");
    }

    private static void AddRegionAndClassFailures(List<string> failures, PageStoreOptions options)
    {
        AddIf(
            failures,
            string.IsNullOrEmpty(options.Region) || !RegionName().IsMatch(options.Region),
            "Region must be a region name such as fr-par.");
        var storageClass = options.OneZoneStorageClass;
        var isOneZone = storageClass == PageStoreOptions.OneZoneInfrequentAccess
            || (options.AllowInsecureEndpoint
                && DevelopmentClasses.Contains(storageClass, StringComparer.Ordinal));
        AddIf(
            failures,
            !isOneZone,
            $"OneZoneStorageClass must be {PageStoreOptions.OneZoneInfrequentAccess} "
                + $"({string.Join(", ", DevelopmentClasses)} with AllowInsecureEndpoint only).");
    }

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
    private static partial Regex BucketName();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex RegionName();
}
