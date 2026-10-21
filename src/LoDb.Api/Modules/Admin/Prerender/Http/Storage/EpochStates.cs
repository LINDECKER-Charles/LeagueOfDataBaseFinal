namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>
/// The words of an epoch of the store, as nginx and the passes see it. An epoch keeps costing
/// until the store has deleted it, whatever its word.
/// </summary>
internal static class EpochStates
{
    /// <summary>The epoch nginx reads (<c>LODB_ARCHIVE_SERVE_EPOCH</c>).</summary>
    public const string Served = "served";

    /// <summary>Newer than the served one, written by the active pass.</summary>
    public const string Writing = "writing";

    /// <summary>
    /// Newer than the served one, written but not served: a complete epoch awaiting the switch,
    /// or the leftovers of a cancelled pass.
    /// </summary>
    public const string Standby = "standby";

    /// <summary>
    /// Older than the served one: nginx no longer reads it, and the lifecycle rule its
    /// retirement adds deletes it, 500,000 objects a day at most. The API cannot see that end.
    /// </summary>
    public const string Retired = "retired";

    public static string Of(int epoch, StorageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (epoch == facts.ServedEpoch)
        {
            return Served;
        }

        if (epoch < facts.ServedEpoch)
        {
            return Retired;
        }

        return epoch == facts.WritingEpoch ? Writing : Standby;
    }
}
