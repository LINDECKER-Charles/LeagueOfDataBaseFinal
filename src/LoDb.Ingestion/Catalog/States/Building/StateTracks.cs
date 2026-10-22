using LoDb.Domain.Catalog;
using LoDb.Domain.States;
using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>
/// The last state of every entity the walk met, and what a version does to them: the one
/// place that decides which states extend, close and open.
/// </summary>
/// <remarks>
/// A version is compared resource by resource; a resource the version lacks leaves its states
/// as they are. An entity whose fingerprint holds extends its state, which writes nothing. One
/// that changes or disappears closes it, its last version being the covered version before.
/// One that changes, is new or comes back opens a state; a return opens one even with the
/// fingerprint it left with.
/// </remarks>
internal sealed class StateTracks
{
    private readonly Dictionary<ResourceType, Dictionary<string, EntityTrack>> byResource = [];

    /// <summary>The tracks of the stored states: each entity's last state, by opener.</summary>
    public static StateTracks Of(IEnumerable<EntityTrack> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var tracks = new StateTracks();
        foreach (var state in states)
        {
            var held = tracks.TracksOf(state.Resource);
            if (!held.TryGetValue(state.Key, out var known) || known.LastOpener < state.LastOpener)
            {
                held[state.Key] = state;
            }
        }

        return tracks;
    }

    /// <summary>
    /// The tracks a version needs the projection of, which a resumed walk has not read yet:
    /// those of the entities it holds whose state closes or comes back there.
    /// </summary>
    public IReadOnlyList<EntityTrack> Unprojected(VersionEntities entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return
        [
            .. entities.Resources.SelectMany(resource => TracksOf(resource).Values
                .Where(track => track.Projection is null
                    && entities.Find(resource, track.Key) is { } entity
                    && (!track.IsOpen || entity.Fingerprint != track.Fingerprint))),
        ];
    }

    /// <summary>
    /// Gives the open states the projection of their entity in <paramref name="entities"/>,
    /// the version a resumed walk starts after.
    /// </summary>
    public void Project(VersionEntities entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        foreach (var resource in entities.Resources)
        {
            var open = TracksOf(resource).Values.Where(static track => track.IsOpen);
            foreach (var track in open)
            {
                track.Projection ??= entities.Find(resource, track.Key)?.Projection;
            }
        }
    }

    /// <summary>
    /// Moves every track to <paramref name="entities"/>' version, whose covered version before
    /// is <paramref name="previous"/> (null for the first covered one).
    /// </summary>
    public VersionChanges Advance(VersionEntities entities, PatchVersion? previous)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var step = new Step(entities.Version, previous);
        foreach (var resource in entities.Resources)
        {
            var tracks = TracksOf(resource);
            var current = entities.Of(resource);
            foreach (var track in tracks.Values)
            {
                Compare(track, current.GetValueOrDefault(track.Key), step);
            }

            foreach (var entity in current.Values.Where(entity => !tracks.ContainsKey(entity.Key)))
            {
                step.Open(entity, track: null, EntityChangeKind.New);
                tracks.Add(entity.Key, EntityTrack.Opened(entity, entities.Version));
            }
        }

        return new VersionChanges
        {
            Version = entities.Version,
            Closed = step.Closed,
            Opened = step.Opened,
        };
    }

    private static void Compare(EntityTrack track, ProjectedEntity? entity, Step step)
    {
        if (entity is null)
        {
            step.CloseIfOpen(track);
            return;
        }

        if (track.IsOpen && entity.Fingerprint == track.Fingerprint)
        {
            track.Projection = entity.Projection;
            return;
        }

        var kind = track.IsOpen ? EntityChangeKind.Changed : EntityChangeKind.Returned;
        step.CloseIfOpen(track);
        step.Open(entity, track, kind);
    }

    private Dictionary<string, EntityTrack> TracksOf(ResourceType resource)
    {
        if (!byResource.TryGetValue(resource, out var tracks))
        {
            tracks = new Dictionary<string, EntityTrack>(StringComparer.Ordinal);
            byResource.Add(resource, tracks);
        }

        return tracks;
    }

    // The transitions of one version; a track is moved as soon as its transition is recorded.
    private sealed class Step(PatchVersion version, PatchVersion? previous)
    {
        public List<ClosedState> Closed { get; } = [];

        public List<OpenedState> Opened { get; } = [];

        public void CloseIfOpen(EntityTrack track)
        {
            if (!track.IsOpen)
            {
                return;
            }

            Closed.Add(new ClosedState
            {
                Resource = track.Resource,
                Key = track.Key,
                Opener = track.LastOpener,
                Last = previous ?? throw new InvalidOperationException(
                    $"An open state of {track.Key} precedes the first covered version."),
                Fingerprint = track.Fingerprint,
            });
            track.IsOpen = false;
        }

        public void Open(ProjectedEntity entity, EntityTrack? track, EntityChangeKind kind)
        {
            Opened.Add(new OpenedState
            {
                Resource = entity.Resource,
                Key = entity.Key,
                Opener = version,
                Fingerprint = entity.Fingerprint,
                Path = entity.Path,
                PreviousOpener = track?.LastOpener,
                Kind = kind,
                ChangedFields = track is null ? [] : ChangedKeys(track, entity),
            });
            track?.Reopen(entity, version);
        }

        private static IReadOnlyList<FieldKey> ChangedKeys(
            EntityTrack track,
            ProjectedEntity entity) =>
            EntityDiff.ChangedKeys(
                track.Projection ?? throw new InvalidOperationException(
                    $"The previous state of {entity.Key} was not projected."),
                entity.Projection);
    }
}
