// Image paths a reduced dataset references, relative to the CDN root: what the ingestion of
// a version fetches (DdragonImagePath in LoDb.Domain). Splash, skin and video art is hotlinked,
// never ingested (UP 8), so it is not collected.

/** Portraits, passives and ability icons of a championFull.json or champion/{id}.json. */
export function championImages(version, file) {
  const paths = [];
  for (const champion of Object.values(file.data ?? {})) {
    pushImage(paths, `${version}/img/champion`, champion.image);
    pushImage(paths, `${version}/img/passive`, champion.passive?.image);
    for (const spell of champion.spells ?? []) {
      pushImage(paths, `${version}/img/spell`, spell.image);
    }
  }
  return paths;
}

/** Item icons of an item.json. */
export function itemImages(version, file) {
  const paths = [];
  for (const item of Object.values(file.data ?? {})) {
    pushImage(paths, `${version}/img/item`, item.image);
  }
  return paths;
}

/** Summoner spell icons of a summoner.json: they share the ability folder. */
export function summonerImages(version, file) {
  const paths = [];
  for (const spell of Object.values(file.data ?? {})) {
    pushImage(paths, `${version}/img/spell`, spell.image);
  }
  return paths;
}

/** Tree and rune icons of a runesReforged.json, under the unversioned img/ root (UP 5). */
export function runeImages(trees) {
  const paths = [];
  for (const tree of trees) {
    pushIcon(paths, tree.icon);
    for (const rune of (tree.slots ?? []).flatMap((slot) => slot.runes ?? [])) {
      pushIcon(paths, rune.icon);
    }
  }
  return paths;
}

function pushImage(paths, folder, image) {
  if (typeof image?.full === 'string' && image.full !== '') {
    paths.push(`${folder}/${image.full}`);
  }
}

function pushIcon(paths, icon) {
  if (typeof icon === 'string' && icon !== '') {
    paths.push(`img/${icon}`);
  }
}
