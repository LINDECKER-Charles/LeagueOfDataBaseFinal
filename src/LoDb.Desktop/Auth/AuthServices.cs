using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Desktop.Auth.Api;
using LoDb.Desktop.Auth.Google;
using LoDb.Desktop.Auth.Tokens;
using LoDb.Desktop.Hosting;
using LoDb.Desktop.Proxy;
using Microsoft.AspNetCore.DataProtection;

namespace LoDb.Desktop.Auth;

/// <summary>Services of the tokens: API client, session, vault and Google sign-in.</summary>
internal static class AuthServices
{
    /// <summary>Key ring name: only this app's protectors read what it protects.</summary>
    public const string ProtectionApplicationName = "LoDb.Desktop";

    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(20);

    // Token and problem answers are small; anything larger is not the API's.
    private const long MaxApiResponseBytes = 1024 * 1024;

    public static IServiceCollection AddDesktopAuth(
        this IServiceCollection services,
        DesktopOptions options)
    {
        services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
        AddProtection(services, options);
        AddAccountApi(services, options);
        services.AddSingleton<RefreshTokenVault>();
        services.AddSingleton<TokenSession>();
        services.AddSingleton<GoogleFlows>();
        services.AddSingleton<GoogleSignIn>();
        services.AddSingleton<AuthEndpoints>();
        services.AddSingleton<GoogleEndpoints>();
        return services;
    }

    public static void MapDesktopAuth(this IEndpointRouteBuilder endpoints) =>
        AuthEndpoints.MapDesktopAuth(endpoints);

    // Not AddDataProtection: its hosted service creates a key at start, which would write
    // into the user's data folder on every smoke check. This provider touches the keys on
    // first use only. DPAPI encrypts them at rest on Windows; macOS and Linux have no
    // equivalent in the framework, and rely on the owner-only data folder.
    private static void AddProtection(IServiceCollection services, DesktopOptions options) =>
        services.AddSingleton<IDataProtectionProvider>(_ => DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(options.DataDirectory, DataDirectory.KeysFolder)),
            protection =>
            {
                protection.SetApplicationName(ProtectionApplicationName);
                if (OperatingSystem.IsWindows())
                {
                    protection.ProtectKeysWithDpapi();
                }
            }));

    private static void AddAccountApi(IServiceCollection services, DesktopOptions options)
    {
        services
            .AddHttpClient(AccountApiClient.ClientName, client =>
            {
                client.BaseAddress = options.ApiOrigin;
                client.Timeout = ApiTimeout;
                client.MaxResponseContentBufferSize = MaxApiResponseBytes;
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    DesktopClient.HeaderName,
                    DesktopClient.ValueOf(options.Version));
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false,
            });
        services.AddSingleton<AccountApiClient>();
    }
}
