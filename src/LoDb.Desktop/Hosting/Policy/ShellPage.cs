namespace LoDb.Desktop.Hosting.Policy;

/// <summary>
/// The shell's index as served: its HTML, and the policy that admits its scripts.
/// </summary>
internal sealed record ShellPage(string Html, string Policy);
