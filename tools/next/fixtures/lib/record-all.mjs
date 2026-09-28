import { championImages, itemImages, runeImages, summonerImages } from './collect-images.mjs';
import {
  ART_PROBES,
  CHAMPIONS,
  ITEMS,
  LANGUAGES,
  LEGACY_VERSION_ENTRIES,
  RUNE_TREES,
  SUMMONERS,
  TRAP_VERSIONS,
} from './recording-plan.mjs';
import { Recording } from './recording.mjs';
import { reduceSkins } from './reduce/reduce-cdragon.mjs';
import {
  championKeys,
  reduceChampions,
  reduceDataMap,
  reduceLanguages,
  reduceRuneTrees,
  reduceVersions,
} from './reduce/reduce-ddragon.mjs';
import * as urls from './upstream-urls.mjs';

const VERSION_SHAPE = /^\d+(?:\.\d+)+$/;
const ART_REASON = 'status only: champion art is hotlinked, never ingested';

/**
 * Records the whole plan. The latest two versions come from the live versions.json; they are
 * the complete ones, whose every ingested image is recorded too.
 * @returns {Promise<{recording: Recording, roles: {latest: string, previous: string}}>}
 */
export async function recordAll(options = {}) {
  const recording = new Recording(options);
  const [latest, previous] = await liveVersions(recording);
  const versions = [latest, previous, ...TRAP_VERSIONS];
  await recording.json(urls.versionsUrl(), (list) =>
    reduceVersions(list, versions, LEGACY_VERSION_ENTRIES),
  );
  await recording.json(urls.languagesUrl(), (list) => reduceLanguages(list, LANGUAGES));

  const datasets = await Promise.all(versions.map((version) => recordVersion(recording, version)));
  const keys = new Set(datasets.flatMap((version) => version.championKeys));
  await Promise.all([
    ...[...versions.map(urls.cdragonPatch), urls.CDRAGON_LATEST].map((patch) =>
      recordSkins(recording, patch, keys),
    ),
    ...datasets.slice(0, 2).map((version) => recordImages(recording, version, allImages)),
    ...datasets.slice(2).map((version) => recordImages(recording, version, runeIcons)),
    recordArtProbes(recording),
  ]);
  return { recording, roles: { latest, previous } };
}

async function liveVersions(recording) {
  const live = await recording.json(urls.versionsUrl());
  const current = (live ?? []).filter((entry) => VERSION_SHAPE.test(entry));
  if (current.length < 2) {
    throw new Error('versions.json is unavailable: nothing recorded.');
  }
  return current.slice(0, 2);
}

async function recordVersion(recording, version) {
  const languages = await Promise.all(
    LANGUAGES.map((language) => recordLanguage(recording, version, language)),
  );
  return {
    version,
    languages,
    championKeys: languages.flatMap((language) => language.champions.flatMap(championKeys)),
  };
}

async function recordLanguage(recording, version, language) {
  const dataset = (file, reduce) =>
    recording.json(urls.datasetUrl(version, language, file), reduce);
  const [full, summary, items, summoners, runes] = await Promise.all([
    dataset('championFull', (file) => reduceChampions(file, CHAMPIONS)),
    dataset('champion', (file) => reduceChampions(file, CHAMPIONS)),
    dataset('item', (file) => reduceDataMap(file, ITEMS)),
    dataset('summoner', (file) => reduceDataMap(file, SUMMONERS)),
    dataset('runesReforged', (trees) => reduceRuneTrees(trees, RUNE_TREES)),
  ]);
  // Without championFull.json the ingestion reads one detail file per champion (UP 3).
  const details =
    full || !summary ? [] : await recordDetails(recording, version, language, summary);
  const champions = [full ?? summary, ...details].filter(Boolean);
  return { champions, items, summoners, runes };
}

async function recordDetails(recording, version, language, summary) {
  const details = await Promise.all(
    Object.values(summary.data).map((champion) =>
      recording.json(urls.championDetailUrl(version, language, champion.id), (file) =>
        reduceChampions(file, CHAMPIONS),
      ),
    ),
  );
  return details.filter(Boolean);
}

async function recordSkins(recording, patch, keys) {
  await recording.json(urls.skinsUrl(patch), (skins) => reduceSkins(skins, keys));
}

async function recordImages(recording, version, collect) {
  const paths = new Set(version.languages.flatMap((language) => collect(version, language)));
  await Promise.all([...paths].map((path) => recording.raw(urls.imageUrl(path))));
}

function allImages({ version }, language) {
  return [
    ...language.champions.flatMap((file) => championImages(version, file)),
    ...(language.items ? itemImages(version, language.items) : []),
    ...(language.summoners ? summonerImages(version, language.summoners) : []),
    ...runeIcons({ version }, language),
  ];
}

// The dead .dds rune icons of 7.22 to 8.7 (UP 5); other old versions ship no runes.
function runeIcons(_, language) {
  return language.runes ? runeImages(language.runes) : [];
}

async function recordArtProbes(recording) {
  const { ids, kinds, skins } = ART_PROBES;
  const paths = ids.flatMap((id) =>
    kinds.flatMap((kind) => skins.map((skin) => `img/champion/${kind}/${id}_${skin}.jpg`)),
  );
  await Promise.all(paths.map((path) => recording.status(urls.imageUrl(path), ART_REASON)));
}
