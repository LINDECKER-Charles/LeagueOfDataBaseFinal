using System.Reflection;
using LoDb.Api.Hosting;

namespace LoDb.Api.Cli;

/// <summary>
/// The commands found under the <c>Cli</c> folder of an assembly, and the choice of the one
/// that the arguments invoke.
/// </summary>
internal sealed class CliCommandCatalog
{
    private const string Folder = "Cli";

    private static readonly MethodInfo DescribeMethod = typeof(CliCommandCatalog).GetMethod(
        nameof(Describe),
        BindingFlags.NonPublic | BindingFlags.Static)!;

    private CliCommandCatalog(IReadOnlyList<CliCommandDescriptor> commands) =>
        Commands = commands;

    /// <summary>The commands, sorted by name.</summary>
    public IReadOnlyList<CliCommandDescriptor> Commands { get; }

    public static CliCommandCatalog Discover(Assembly assembly) =>
        new(ConventionNamespace.TypesUnder(assembly, Folder)
            .Where(IsCommand)
            .Select(DescribeType)
            .OrderBy(static command => command.Name, StringComparer.Ordinal)
            .ToArray());

    /// <summary>
    /// The command whose words start the arguments; the longest wins, so "catalog export"
    /// is chosen over a "catalog" command.
    /// </summary>
    public CliInvocation? Match(IReadOnlyList<string> arguments)
    {
        var command = Commands
            .Where(candidate => StartsWith(arguments, candidate.Words))
            .MaxBy(static candidate => candidate.Words.Count);
        return command is null
            ? null
            : new CliInvocation(command, arguments.Skip(command.Words.Count).ToArray());
    }

    private static bool IsCommand(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && typeof(ICliCommand).IsAssignableFrom(type);

    private static bool StartsWith(IReadOnlyList<string> arguments, IReadOnlyList<string> words) =>
        words.Count > 0
        && arguments.Count >= words.Count
        && arguments.Take(words.Count).SequenceEqual(words, StringComparer.Ordinal);

    // The name and the host requirement are static members: reading them needs the type as a
    // generic argument, which reflection supplies here.
    private static CliCommandDescriptor DescribeType(Type type) =>
        (CliCommandDescriptor)DescribeMethod.MakeGenericMethod(type).Invoke(null, null)!;

    private static CliCommandDescriptor Describe<TCommand>()
        where TCommand : ICliCommand =>
        new(TCommand.Name, TCommand.RequiresHost, typeof(TCommand));
}
