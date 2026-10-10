using LoDb.Api.Modules.Accounts.Security.Admission;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>
/// What protects the accounts' expensive and sensitive calls: the admission to password
/// hashing, with its bounds read from the configuration.
/// </summary>
/// <remarks>The accounts module calls it once. It does no I/O and throws nothing.</remarks>
internal static class AccountsProtectionRegistration
{
    public static IServiceCollection AddAccountsProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HashingAdmissionOptions>()
            .Bind(configuration.GetSection(HashingAdmissionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                static options => options.HasPositiveDelays(),
                $"{HashingAdmissionOptions.SectionName} needs a positive MaxWait and RetryAfter.")
            .ValidateOnStart();
        services.TryAddSingleton<HashingAdmission>();
        return services;
    }
}
