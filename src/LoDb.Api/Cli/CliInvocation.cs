namespace LoDb.Api.Cli;

/// <summary>
/// The command the arguments name, and the arguments that follow its name.
/// </summary>
internal sealed record CliInvocation(CliCommandDescriptor Command, IReadOnlyList<string> Arguments);
