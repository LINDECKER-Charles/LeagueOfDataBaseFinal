namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>
/// The one writer of the index of states: the build command, the promotion and the
/// reconciliation all catch it up through it, under the advisory lock <c>catalog:states</c>.
/// </summary>
/// <remarks>
/// <para>
/// The index covers the longest run of consecutive listed versions whose en_US datasets are
/// stored, ending at the latest promoted version; a version whose four datasets are
/// definitively absent counts as unlisted. Each version is compared to the covered version
/// before it, and written in one transaction: an interrupted update resumes where it stopped.
/// </para>
/// <para>
/// A build starts a new generation when none of the compiled fingerprint scheme is current,
/// when the coverage start moved, or when asked to; a promotion and the reconciliation build
/// only when none is current, and otherwise extend it. The copy of the index the process
/// holds is invalidated after every update.
/// </para>
/// </remarks>
public interface IStateIndexUpdater
{
    /// <exception cref="Egress.Errors.EgressException">
    /// Data Dragon's list of versions, or a missing en_US dataset, could not be read: nothing
    /// past the last version written is indexed; the next update tries again.
    /// </exception>
    Task<StateIndexReport> UpdateAsync(
        StateIndexRequest request,
        CancellationToken cancellationToken);
}
