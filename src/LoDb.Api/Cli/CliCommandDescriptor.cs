namespace LoDb.Api.Cli;

/// <summary>
/// A discovered command: its name, whether it needs the host, and its type.
/// </summary>
internal sealed record CliCommandDescriptor(string Name, bool RequiresHost, Type CommandType)
{
    private const char WordSeparator = ' ';

    /// <summary>Words of <see cref="Name"/>, matched one by one against the arguments.</summary>
    public IReadOnlyList<string> Words { get; } =
        Name.Split(WordSeparator, StringSplitOptions.RemoveEmptyEntries);
}
