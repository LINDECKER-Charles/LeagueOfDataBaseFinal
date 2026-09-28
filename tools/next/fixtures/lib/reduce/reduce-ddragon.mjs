// Reductions of the Data Dragon documents: the kept entries stay verbatim, the others go.
// Each function returns the reduced document and a note saying what was dropped, which the
// index records next to the response.

/** versions.json: the recorded versions, plus a few legacy `lolpatch_*` entries (UP 4). */
export function reduceVersions(versions, recorded, legacyEntries) {
  const kept = new Set(recorded);
  let legacyLeft = legacyEntries;
  const reduced = versions.filter((entry) => {
    if (kept.has(entry)) {
      return true;
    }
    if (entry.startsWith('lolpatch_') && legacyLeft > 0) {
      legacyLeft--;
      return true;
    }
    return false;
  });
  return { document: reduced, reduction: 'recorded versions and first lolpatch_* entries only' };
}

/** languages.json: the recorded languages, in upstream order. */
export function reduceLanguages(languages, recorded) {
  const kept = new Set(recorded);
  return {
    document: languages.filter((language) => kept.has(language)),
    reduction: 'recorded languages only',
  };
}

/**
 * championFull.json, champion.json and champion/{id}.json: the kept champions, matched without
 * case. Their `recommended` item sets go: the ingestion never reads them and they weigh most
 * of an old entry.
 */
export function reduceChampions(file, championIds) {
  const kept = new Set(championIds.map((id) => id.toLowerCase()));
  const data = pickEntries(file.data, (id) => kept.has(id.toLowerCase()));
  for (const champion of Object.values(data)) {
    delete champion.recommended;
  }
  const document = { ...file, data };
  if (file.keys) {
    document.keys = pickEntries(file.keys, (_, id) => kept.has(String(id).toLowerCase()));
  }
  return { document, reduction: `champions ${championIds.join(', ')}; recommended dropped` };
}

/** item.json and summoner.json: the kept entries of the id-keyed `data` map. */
export function reduceDataMap(file, ids) {
  const kept = new Set(ids);
  return {
    document: { ...file, data: pickEntries(file.data, (id) => kept.has(id)) },
    reduction: 'listed entries only',
  };
}

/** runesReforged.json: the kept trees, whole. */
export function reduceRuneTrees(trees, treeIds) {
  const kept = new Set(treeIds);
  return {
    document: trees.filter((tree) => kept.has(tree.id)),
    reduction: `trees ${treeIds.join(', ')} only`,
  };
}

/** Champion `key`s ("103") of a reduced champion file, for the CommunityDragon reduction. */
export function championKeys(file) {
  return Object.values(file.data ?? {})
    .map((champion) => champion.key)
    .filter((key) => typeof key === 'string' && key !== '');
}

function pickEntries(map, keep) {
  return Object.fromEntries(Object.entries(map ?? {}).filter(([key, value]) => keep(key, value)));
}
