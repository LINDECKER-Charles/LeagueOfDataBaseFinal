using System.Reflection;
using LoDb.Api.Hosting;

namespace LoDb.Api.Cli;

/// <summary>
/// Runs <c>dotnet LoDb.Api.dll &lt;command&gt; [arguments]</c>, chosen before any web host
/// exists: no command opens the API port or the metrics port.
/// </summary>
internal static partial class CliRunner
{
    // WebApplication reads its environment from ASPNETCORE_ENVIRONMENT; the command host
    // must load the same settings files as the API it serves.
    private const string WebEnvironmentPrefix = "ASPNETCORE_";
    private const char OptionMark = '-';
    private const char SwitchMark = '/';
    private const char AssignmentMark = '=';

    /// <summary>Whether the arguments name a command rather than host settings.</summary>
    /// <remarks>
    /// Settings come as <c>--key=value</c>, <c>/key</c> or <c>key=value</c> (the test host
    /// passes its own that way), so only a bare first word is a command.
    /// </remarks>
    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0
        && args[0] is { Length: > 0 } first
        && first[0] is not OptionMark and not SwitchMark
        && !first.Contains(AssignmentMark, StringComparison.Ordinal);

    public static async Task<int> RunAsync(
        string[] args,
        Assembly assembly,
        Action<IServiceCollection, IConfiguration> addServices)
    {
        var catalog = CliCommandCatalog.Discover(assembly);
        if (catalog.Match(args) is not { } invocation)
        {
            var known = string.Join(", ", catalog.Commands.Select(static command => command.Name));
            var requested = string.Join(' ', args);
            await Console.Error.WriteLineAsync(
                $"Unknown command '{requested}'. Commands: {known}.");
            return CliExitCodes.Usage;
        }

        using var shutdown = new ShutdownSignal();
        try
        {
            return invocation.Command.RequiresHost
                ? await RunInHostAsync(invocation, addServices, shutdown.Token)
                : await RunAloneAsync(invocation, shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.Token.IsCancellationRequested)
        {
            return CliExitCodes.Failure;
        }
    }

    private static Task<int> RunAloneAsync(
        CliInvocation invocation,
        CancellationToken cancellationToken)
    {
        var command = (ICliCommand)Activator.CreateInstance(invocation.Command.CommandType)!;
        return command.ExecuteAsync(invocation.Arguments, cancellationToken);
    }

    // The host is built for its services and never started: no hosted service, no server.
    private static async Task<int> RunInHostAsync(
        CliInvocation invocation,
        Action<IServiceCollection, IConfiguration> addServices,
        CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables(WebEnvironmentPrefix);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [.. invocation.Arguments],
            Configuration = configuration,
        });
        builder.AddLoDbCommon();
        addServices(builder.Services, builder.Configuration);
        builder.Services.AddTransient(invocation.Command.CommandType);

        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        return await ExecuteLoggedAsync(scope.ServiceProvider, invocation, cancellationToken);
    }

    private static async Task<int> ExecuteLoggedAsync(
        IServiceProvider services,
        CliInvocation invocation,
        CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(CliRunner).FullName!);
        var command = (ICliCommand)services.GetRequiredService(invocation.Command.CommandType);
        try
        {
            return await command.ExecuteAsync(invocation.Arguments, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // One JSON line instead of the runtime's multi-line crash report.
            LogCommandFailed(logger, invocation.Command.Name, exception);
            return CliExitCodes.Failure;
        }
    }

    [LoggerMessage(
        EventName = "cli.command.failed",
        Level = LogLevel.Error,
        Message = "Command {Command} failed.")]
    private static partial void LogCommandFailed(
        ILogger logger,
        string command,
        Exception exception);
}
