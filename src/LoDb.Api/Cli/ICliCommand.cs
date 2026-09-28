namespace LoDb.Api.Cli;

/// <summary>
/// A sub-command of <c>dotnet LoDb.Api.dll &lt;command&gt; [arguments]</c>, discovered under
/// <c>Cli/&lt;Zone&gt;/</c>.
/// </summary>
/// <remarks>
/// The name is static so the runner picks a command without creating anything. A command
/// runs in a generic host, never started, that holds the same registrations as the web host;
/// one that needs no service opts out, and the host is then not even built.
/// </remarks>
internal interface ICliCommand
{
    /// <summary>Words that invoke the command, separated by one space: "catalog export".</summary>
    static abstract string Name { get; }

    /// <summary>False for a command created without the host and its services.</summary>
    static virtual bool RequiresHost => true;

    /// <summary>Runs the command with the arguments that follow its name.</summary>
    /// <returns>The process exit code, one of <see cref="CliExitCodes"/>.</returns>
    Task<int> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
