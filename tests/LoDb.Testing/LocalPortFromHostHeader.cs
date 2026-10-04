using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace LoDb.Testing;

/// <summary>
/// Gives each in-memory request the local port named in its <c>Host</c> header.
/// </summary>
/// <remarks>
/// The in-memory server has no socket, so every request arrives on port 0. With this filter a
/// client whose base address is <c>http://localhost:9464</c> reaches the metrics port exactly
/// as a scrape would; a base address without a port keeps port 0.
/// </remarks>
internal sealed class LocalPortFromHostHeader : IStartupFilter
{
    private const int NoPort = 0;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(static (context, nextMiddleware) =>
            {
                context.Connection.LocalPort = context.Request.Host.Port ?? NoPort;
                return nextMiddleware(context);
            });
            next(app);
        };
}
