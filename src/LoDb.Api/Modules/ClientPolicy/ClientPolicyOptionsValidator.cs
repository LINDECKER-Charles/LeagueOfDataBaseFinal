using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>
/// Refuses at startup a version that does not parse, or a minimum above the latest release:
/// every app would then be told to update to a version that does not exist.
/// </summary>
internal sealed class ClientPolicyOptionsValidator : IValidateOptions<ClientPolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, ClientPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = Enum.GetValues<ClientPlatform>()
            .SelectMany(platform => Check(platform, options.Of(platform)))
            .ToList();
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> Check(
        ClientPlatform platform,
        PlatformVersionOptions versions)
    {
        Version? minimum = null;
        Version? latest = null;
        if (versions.MinimumVersion is { } text && !AppVersion.TryParse(text, out minimum))
        {
            yield return $"{platform}:MinimumVersion must read as 1.2.3, not '{text}'.";
        }

        if (versions.LatestVersion is { } last && !AppVersion.TryParse(last, out latest))
        {
            yield return $"{platform}:LatestVersion must read as 1.2.3, not '{last}'.";
        }

        if (minimum is not null && latest is not null && minimum > latest)
        {
            yield return $"{platform}:MinimumVersion must not exceed LatestVersion.";
        }
    }
}
