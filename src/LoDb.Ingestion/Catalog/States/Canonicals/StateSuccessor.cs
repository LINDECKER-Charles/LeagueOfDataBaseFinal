using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Ingestion.Catalog.States.Canonicals;

/// <summary>
/// What ended a closed state: the entity's next state opening at the version after it, or
/// that version no longer holding the entity.
/// </summary>
/// <param name="Version">The covered version after the state's last one.</param>
/// <param name="Next">The state that opens there; null when the entity was retired.</param>
public sealed record StateSuccessor(PatchVersion Version, IndexedState? Next)
{
    public bool IsRetirement => Next is null;
}
