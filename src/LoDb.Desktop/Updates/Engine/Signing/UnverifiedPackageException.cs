namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>
/// A downloaded package that a signed feed does not vouch for: never handed to the updater.
/// </summary>
internal sealed class UnverifiedPackageException(string message) : Exception(message);
