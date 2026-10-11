using Microsoft.AspNetCore.HostFiltering;

namespace LoDb.Api.Hosting;

/// <summary>
/// <c>AllowedHosts</c>: the hosts the API answers; any other <c>Host</c> header gets a 400.
/// </summary>
/// <remarks>
/// <para>
/// A served host lists its public hosts (the site and the API subdomain), the alias the
/// renderer and nginx call the API by and the loopback of the healthcheck, separated by
/// commas, semicolons or spaces: Compose joins the lists of the .env, whose hosts Caddy and
/// the deploy job also take separated by spaces. <c>*</c>, the default, lets every host
/// through, as the local stacks need. Links of the e-mails, return URLs and the own-origin
/// check of the forgery guard then never rest on a host the edge did not route.
/// </para>
/// <para>
/// ASP.NET Core filters hosts before any middleware, the metrics port included, whose
/// scraper names the container by its address. The framework's filter is taken out and the
/// same middleware runs after the metrics port and the network gate instead.
/// </para>
/// </remarks>
internal static class HostFilteringSetup
{
    /// <summary>The setting, under the name ASP.NET Core gives it.</summary>
    public const string Key = "AllowedHosts";

    private const string AnyHost = "*";
    private const string FrameworkFilter = "Microsoft.AspNetCore.HostFilteringStartupFilter";
    private static readonly char[] Separators = [',', ';', ' ', '\t'];

    public static IServiceCollection AddLoDbHostFiltering(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        foreach (var filter in services.Where(IsFrameworkFilter).ToList())
        {
            services.Remove(filter);
        }

        services.AddOptions<HostFilteringOptions>()
            .Configure(options => options.AllowedHosts = Parse(configuration[Key]));
        return services;
    }

    public static IApplicationBuilder UseLoDbHostFiltering(this IApplicationBuilder app) =>
        app.UseHostFiltering();

    /// <summary>The hosts of <paramref name="setting"/>; every host when it names none.</summary>
    public static List<string> Parse(string? setting)
    {
        var hosts = (setting ?? string.Empty)
            .Split(
                Separators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return hosts.Count == 0 ? [AnyHost] : hosts;
    }

    private static bool IsFrameworkFilter(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IStartupFilter)
        && descriptor.ImplementationType?.FullName == FrameworkFilter;
}
