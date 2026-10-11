// What the bench's requests are built from, read once from the stack before a run (the k6
// setup): its versions, locales and languages, and the entries of the latest patch, whose
// ids hold across patches.
// Plain module: imported by the k6 scripts and by node --test alike.

const RESOURCES = ['champions', 'items', 'runes', 'summoners'];

// The former site's segments: plural for a list, singular before a name, items as objects.
const LEGACY_SEGMENTS = {
  champions: { list: 'champions', detail: 'champion' },
  items: { list: 'objects', detail: 'object' },
  runes: { list: 'runes', detail: 'rune' },
  summoners: { list: 'summoners', detail: 'summoner' },
};

// A rune page shows a whole path: the list's paths are its pages, and the former site named
// a path by its key ("Domination").
function entriesOf(resource, list) {
  return resource === 'runes' ? list.trees : list.entries;
}

function legacyNameOf({ resource, entry }) {
  const name = resource === 'runes' ? entry.key : entry.id;
  return `${LEGACY_SEGMENTS[resource].detail}/${name}`;
}

/**
 * The context of a run, from `/api/meta` and the latest patch's en_US list of each resource
 * (`lists.champions`…): the pages, API reads and former URLs the scenarios walk through.
 */
export function contextOf(meta, lists) {
  if (!meta.latest) {
    throw new Error('The stack has not promoted a version yet: wait for its ingestion.');
  }
  const entries = RESOURCES.flatMap((resource) =>
    entriesOf(resource, lists[resource]).map((entry) => ({ resource, entry })),
  );
  return {
    latest: meta.latest,
    archived: meta.versions.filter((version) => version !== meta.latest),
    locales: meta.locales.map(({ locale }) => locale),
    languages: meta.languages,
    lists: RESOURCES,
    pages: entries.map(({ entry }) => entry.canonicalPath),
    reads: entries.map(({ resource, entry }) => `${resource}/${entry.id}`),
    legacyLists: RESOURCES.map((resource) => LEGACY_SEGMENTS[resource].list),
    legacyNames: entries.map(legacyNameOf),
  };
}
