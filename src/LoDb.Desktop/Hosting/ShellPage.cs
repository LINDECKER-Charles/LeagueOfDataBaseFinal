namespace LoDb.Desktop.Hosting;

/// <summary>
/// The shell's index as served: its HTML, and the policy that admits its scripts.
/// </summary>
internal sealed record ShellPage(string Html, string Policy);
