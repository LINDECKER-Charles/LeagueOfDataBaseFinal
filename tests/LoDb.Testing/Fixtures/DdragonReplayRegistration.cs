using LoDb.Ingestion.Egress;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Testing.Fixtures;

/// <summary>
/// Plugs a <see cref="FixtureReplayHandler"/> under the egress client.
/// </summary>
/// <remarks>
/// Only the primary handler changes: the allow-list, the redirects, the retries and the
/// circuit breakers of the egress zone all run as in production.
/// </remarks>
public static class DdragonReplayRegistration
{
    /// <summary>
    /// Replays the recording for the <c>ddragon</c> client. Call it after
    /// <c>AddLoDbEgress</c>, which it overrides.
    /// </summary>
    public static IServiceCollection AddDdragonFixtureReplay(
        this IServiceCollection services,
        FixtureReplayHandler replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        services.AddHttpClient(EgressRegistration.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => replay);
        return services;
    }
}
