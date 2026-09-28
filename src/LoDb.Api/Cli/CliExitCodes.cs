namespace LoDb.Api.Cli;

/// <summary>
/// Exit codes of the sub-commands.
/// </summary>
internal static class CliExitCodes
{
    public const int Success = 0;

    /// <summary>The command ran and failed; Docker reads it as an unhealthy probe.</summary>
    public const int Failure = 1;

    /// <summary>The arguments name no known command.</summary>
    public const int Usage = 2;
}
