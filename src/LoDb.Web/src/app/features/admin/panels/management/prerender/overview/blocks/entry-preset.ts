import type { LaunchPreset } from '../../common/scope/launch-preset';

/** The entries of the page that start a launch from nothing but an epoch (§ 3.1). */
export type EntryOrigin = 'header' | 'idle' | 'complete-epoch';

/**
 * The preset of an entry of the page (§ 3.1): « Lancer une passe » picks nothing; « Les
 * compléter » and « Compléter l'epoch N » take the patches with a gap in `epoch`, resolved
 * once the dialog reads its coverage (null for the served epoch). Every locale and type,
 * leaving out what the epoch already holds.
 */
export function entryPreset(origin: EntryOrigin, epoch: number | null): LaunchPreset {
  return {
    origin,
    versions: origin === 'header' ? [] : 'incomplete',
    priority: [],
    locales: null,
    pageTypes: null,
    epoch,
    skipComplete: true,
    fromPass: null,
  };
}
