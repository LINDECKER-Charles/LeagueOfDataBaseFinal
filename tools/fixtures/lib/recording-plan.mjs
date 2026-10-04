// What the recorder captures: the versions, languages and entries the ingestion tests need,
// and nothing more, so that the recorded set stays well under its size budget.

/**
 * Versions with a known trap (heritage.md § 5), recorded on top of the latest two:
 * - 8.7.1: last version whose rune icons are dead `.dds` paths (UP 5);
 * - 7.22.1: first version with runesReforged.json, in en_US only (UP 1);
 * - 7.21.1: no runesReforged.json in any language (UP 1);
 * - 3.13.24: first version whose skins carry `num`;
 * - 3.13.8: championFull.json without skin `num` (UP 3);
 * - 3.6.14: no championFull.json and most detail files in 403 (UP 3);
 * - 0.151.2: no championFull.json, no `partype`, skins without id nor `num` (UP 3).
 */
export const TRAP_VERSIONS = Object.freeze([
  '8.7.1',
  '7.22.1',
  '7.21.1',
  '3.13.24',
  '3.13.8',
  '3.6.14',
  '0.151.2',
]);

/** Languages of the recording; the ones a version lacks are recorded as its 403 answers. */
export const LANGUAGES = Object.freeze(['en_US', 'fr_FR', 'ko_KR', 'ar_AE', 'zh_CN']);

/** Legacy `lolpatch_*` entries kept in versions.json, enough to exercise their filter (UP 4). */
export const LEGACY_VERSION_ENTRIES = 2;

/** The Data Dragon dataset files read per version and language. */
export const DATASET_FILES = Object.freeze([
  'championFull',
  'champion',
  'item',
  'summoner',
  'runesReforged',
]);

/**
 * Champions kept, matched without case: the id was "FiddleSticks" before it became
 * "Fiddlesticks" (UP 7). Ahri has chromas, MonkeyKing's id differs from its name, Garen has
 * no resource and Teemo's R stores charges.
 */
export const CHAMPIONS = Object.freeze(['Ahri', 'Fiddlesticks', 'MonkeyKing', 'Garen', 'Teemo']);

/**
 * Items kept when a version carries them: a recipe (1001, 1042 → 3006), classic twins
 * (1004 ↔ 771004) and a reused id (3001 vs 773001, UP 6), debris (2008, 226660, 772139,
 * 772140, 7050) and marked-up names (3901-3903, UP 10), owner restrictions (3599, 3371).
 */
export const ITEMS = Object.freeze([
  '1001',
  '1004',
  '1042',
  '2003',
  '2008',
  '3001',
  '3006',
  '3078',
  '3371',
  '3599',
  '3901',
  '3902',
  '3903',
  '7050',
  '226660',
  '771004',
  '772139',
  '772140',
  '773001',
]);

/** Summoner spells kept: modern ones, their JADE twins (UP 6) and internal-mode ones (UP 11). */
export const SUMMONERS = Object.freeze([
  'SummonerExhaust',
  'SummonerFlash',
  'SummonerFlash_Jade',
  'SummonerHeal',
  'SummonerHeal_Jade',
  'SummonerSnowball',
]);

/** Rune trees kept: Precision and Domination, with every slot and rune. */
export const RUNE_TREES = Object.freeze([8000, 8100]);

/**
 * Champion art spellings whose status documents UP 7: the public id loses the newer skins and
 * the whole centered family, the internal one answers them all. Only the status matters: the
 * art is hotlinked, never ingested, so its body is not recorded.
 */
export const ART_PROBES = Object.freeze({
  ids: ['FiddleSticks', 'Fiddlesticks'],
  kinds: ['splash', 'centered'],
  skins: [0, 27],
});
