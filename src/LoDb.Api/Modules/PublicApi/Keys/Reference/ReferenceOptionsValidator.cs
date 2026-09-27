using LoDb.Api.Modules.Accounts.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Keys.Reference;

/// <summary>
/// Refuses at startup a documented base URL that is not a bare http(s) origin, which every
/// sample of <c>/developers</c> would carry.
/// </summary>
internal sealed class ReferenceOptionsValidator : IValidateOptions<ReferenceOptions>
{
    public ValidateOptionsResult Validate(string? name, ReferenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var unset = string.IsNullOrWhiteSpace(options.BaseUrl);
        return unset || WebOrigin.TryParse(options.BaseUrl, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"BaseUrl must read as https://host[:port], not '{options.BaseUrl}'.");
    }
}
