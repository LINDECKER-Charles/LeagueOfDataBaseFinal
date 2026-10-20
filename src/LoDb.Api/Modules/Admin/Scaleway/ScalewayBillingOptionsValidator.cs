using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// Refuses at startup an id that is not a UUID, and a key set without the three ids. Without
/// the key, nothing is required: the bill stays off.
/// </summary>
internal sealed class ScalewayBillingOptionsValidator : IValidateOptions<ScalewayBillingOptions>
{
    public ValidateOptionsResult Validate(string? name, ScalewayBillingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var isRequired = options.IsConfigured;
        string[] failures =
        [
            .. new[]
            {
                IdFailure(nameof(options.OrganizationId), options.OrganizationId, isRequired),
                IdFailure(nameof(options.BackupsProjectId), options.BackupsProjectId, isRequired),
                IdFailure(nameof(options.PagesProjectId), options.PagesProjectId, isRequired),
            }.OfType<string>(),
        ];
        return failures.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    // A typo in an id is refused even while the key is unset: it would only show once the key
    // is.
    private static string? IdFailure(string key, string? value, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(value) && !isRequired)
        {
            return null;
        }

        return Guid.TryParse(value, out _)
            ? null
            : $"{key} must be a UUID: required once SecretKey is set.";
    }
}
