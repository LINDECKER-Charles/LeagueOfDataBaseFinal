namespace LoDb.Api.Hosting.Errors;

/// <summary>How the database failed a request.</summary>
internal enum DatabaseFailure
{
    /// <summary>
    /// Every connection of the pool stayed busy for the whole wait: load the API sheds.
    /// </summary>
    PoolExhausted,

    /// <summary>
    /// The server could not be reached, did not answer in time, or refused the session: an
    /// outage.
    /// </summary>
    Unavailable,
}
