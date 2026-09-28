using System.Globalization;
using System.Net;
using LoDb.Api.Hosting;
using LoDb.Api.Hosting.Health;

namespace LoDb.Api.Cli.Health;

/// <summary>
/// <c>healthcheck</c>: the Docker probe of the chiseled image, which has neither shell nor
/// curl. Exit code 0 when <c>/healthz</c> answers 200 on the local API port, 1 otherwise.
/// </summary>
/// <remarks>
/// It runs without the host, so it opens no port and resolves no service. The port comes
/// from <c>LoDb:Hosting:HttpPort</c> in the environment or the arguments, as for the API.
/// </remarks>
internal sealed class HealthcheckCommand : ICliCommand
{
    private const int TimeoutSeconds = 3;
    private const int LowestPort = 1;

    public static string Name => "healthcheck";

    public static bool RequiresHost => false;

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (ReadPort(arguments) is not { } port)
        {
            return CliExitCodes.Failure;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
        try
        {
            using var response = await client.GetAsync(LivenessUri(port), cancellationToken);
            return response.StatusCode == HttpStatusCode.OK
                ? CliExitCodes.Success
                : CliExitCodes.Failure;
        }
        catch (HttpRequestException)
        {
            return CliExitCodes.Failure;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's timeout, not a shutdown: the API is not answering.
            return CliExitCodes.Failure;
        }
    }

    private static Uri LivenessUri(int port) =>
        new UriBuilder(
            Uri.UriSchemeHttp,
            IPAddress.Loopback.ToString(),
            port,
            HealthRegistration.LivenessPath).Uri;

    private static int? ReadPort(IReadOnlyList<string> arguments)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine([.. arguments])
            .Build();
        var value = configuration[HostingOptions.HttpPortKey];
        if (value is null)
        {
            return HostingOptions.DefaultHttpPort;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is >= LowestPort and <= IPEndPoint.MaxPort
            ? port
            : null;
    }
}
