namespace LoDb.Desktop.Auth.Google;

/// <summary>Where the last Google sign-in stands, as the front polls it.</summary>
internal enum GoogleStage
{
    /// <summary>No sign-in running; the last one, if any, succeeded.</summary>
    Idle,

    /// <summary>Google's page is open in the browser, or the code is being exchanged.</summary>
    Pending,

    /// <summary>The last sign-in failed; the failure code says why.</summary>
    Failed,
}
