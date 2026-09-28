using System.Reflection;
using LoDb.Api.Hosting;
using LoDb.Api.Hosting.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Workers;

/// <summary>
/// Registers every <see cref="BackgroundService"/> found under the <c>Workers</c> folder.
/// </summary>
/// <remarks>
/// <c>LoDb:Workers:Enabled=false</c> switches them all off: integration tests must never
/// start a patch watch or reach the network. Only an explicit <c>false</c> does; a missing
/// value keeps them on, the production default. The build-time OpenAPI generator starts the
/// host too, before any setting can reach it, so it switches them off by itself.
/// </remarks>
internal static class ConventionalWorkers
{
    public const string EnabledKey = "LoDb:Workers:Enabled";
    private const string Folder = "Workers";

    public static IServiceCollection AddConventionalWorkers(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        if (!IsEnabled(configuration))
        {
            return services;
        }

        foreach (var worker in Discover(assembly))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), worker));
        }

        return services;
    }

    /// <summary>The background services of the assembly, sorted by full name.</summary>
    public static IReadOnlyList<Type> Discover(Assembly assembly) =>
        ConventionNamespace.TypesUnder(assembly, Folder)
            .Where(static type => type is { IsClass: true, IsAbstract: false }
                && type.IsSubclassOf(typeof(BackgroundService)))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static bool IsEnabled(IConfiguration configuration) =>
        !OpenApiDocuments.IsBuildTimeGeneration
        && !(bool.TryParse(configuration[EnabledKey], out var enabled) && !enabled);
}
