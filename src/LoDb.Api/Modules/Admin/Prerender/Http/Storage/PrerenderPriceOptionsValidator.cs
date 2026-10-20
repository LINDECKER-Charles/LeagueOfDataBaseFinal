using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>Refuses at startup a price the panel could not show.</summary>
internal sealed class PrerenderPriceOptionsValidator : IValidateOptions<PrerenderPriceOptions>
{
    public ValidateOptionsResult Validate(string? name, PrerenderPriceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.PricePerGbMonthOneZone < 0)
        {
            failures.Add("PricePerGbMonthOneZone must not be negative.");
        }

        if (options.PricePerGbMonthMultiAz < 0)
        {
            failures.Add("PricePerGbMonthMultiAz must not be negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
