namespace LoDb.Api.Modules.Profiles.Deletion;

/// <summary>What an account types to confirm its erasure, one of three variants.</summary>
internal enum DeletionConfirmation
{
    /// <summary>
    /// A Google account types the phrase of its locale: its credential is Google's, so a
    /// password is impossible or not its own.
    /// </summary>
    Phrase,

    /// <summary>An account with a password types it.</summary>
    Password,

    /// <summary>
    /// An account with neither confirms nothing: the right to erasure is never blocked, and
    /// the forgery guard still stands.
    /// </summary>
    None,
}
