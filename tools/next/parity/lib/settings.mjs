// Sample, locations and compose commands of the parity collection (L1.8).
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const repoRoot = path.resolve(fileURLToPath(new URL('../../../..', import.meta.url)));
export const toolDir = path.join(repoRoot, 'tools/next/parity');
export const runsDir = path.join(toolDir, '.runs');

/** Languages of the sample: Latin, accented, Hangul, right-to-left and Han scripts. */
export const defaultLanguages = ['en_US', 'fr_FR', 'ko_KR', 'ar_AE', 'zh_CN'];
export const defaultLatest = 10;

/**
 * Versions whose files broke the legacy stack once: first patch, 3.x champion ids, the last
 * patch without runesReforged, the first with it, and the 8.7.1 gaps.
 */
export const trapVersions = ['8.7.1', '7.22.1', '7.21.1', '3.13.24', '0.151.2'];

/**
 * Champions whose detail page is visited on the legacy stack, which only stores details,
 * ability icons and chromas when a page asks for them: the first ones of every list, the
 * id-name trap (MonkeyKing), and both casings Fiddlesticks had over the years. One absent
 * from a version sends back to the list: the visit stores nothing.
 */
export const detailChampions = [
  'Aatrox', 'Ahri', 'Annie', 'Fiddlesticks', 'FiddleSticks', 'MonkeyKing', 'Kaisa',
];

export const versionsUrl = 'https://ddragon.leagueoflegends.com/api/versions.json';
export const legacyBaseUrl = 'http://localhost:8080';

/** The legacy stack: the compose files archived in legacy/, project `lodb`. */
export const legacyCompose = ['compose', '--project-directory', 'legacy'];

/** The new stack, one instance only. */
export const nextCompose = [
  'compose', '-p', 'lodb-next', '-f', 'compose.next.yaml', '-f', 'compose.next.override.yaml',
];

export const legacyStorageVolume = 'lodb_storage';
export const legacyScriptDir = '/tmp/lodb-parity';
export const legacyOutputDir = '/tmp/lodb-parity-out';

/** Memory the legacy CLI needs on the full sample: its 256M default runs out (debug cache). */
export const legacyMemoryLimit = '2G';
