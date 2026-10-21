import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import type { LaunchPreset } from '../common/scope/launch-preset';

/** What a relaunch reads beyond the pass: what its list views leave out. */
export interface RelaunchSources {
  /** The versions the pass lists, from a complete read of its progress; null for none. */
  readonly scopeVersions: readonly string[] | null;
  /** The older patches of the coverage, to turn a range from before the lists into a list. */
  readonly patches: readonly string[];
  /** The epoch nginx serves: a pass of a retired epoch is relaunched into it. */
  readonly servedEpoch: number;
}

// Versions compare segment by segment, as numbers: 16.10.1 comes after 16.9.1.
function compare(one: string, other: string): number {
  const left = one.split('.').map(Number);
  const right = other.split('.').map(Number);
  for (let index = 0; index < Math.max(left.length, right.length); index++) {
    const difference = (left[index] ?? 0) - (right[index] ?? 0);
    if (difference !== 0) {
      return difference;
    }
  }
  return 0;
}

// The older patches of a range from before the lists, open at either end.
function rangeOf(pass: PrerenderPassView, patches: readonly string[]): readonly string[] {
  const { oldest, newest } = pass.scope;
  return patches.filter(
    (version) =>
      (!oldest || compare(version, oldest) >= 0) && (!newest || compare(version, newest) <= 0),
  );
}

function versionsOf(pass: PrerenderPassView, sources: RelaunchSources): LaunchPreset['versions'] {
  const { scope } = pass;
  if (scope.oldest || scope.newest) {
    return rangeOf(pass, sources.patches);
  }
  if (scope.versionCount === null || scope.versionCount === undefined) {
    return 'all';
  }
  return sources.scopeVersions ?? [];
}

/**
 * « Relancer ce périmètre » (§ 5.4): the perimeter of a pass again, whole. Its versions (every
 * older patch, its list read from the complete progress, or its range from before the lists
 * turned into the older patches it spans), its locales, types and priorities; its epoch,
 * unless retired since, then the served one. Nothing is skipped: it renders again what the
 * pass rendered.
 */
export function relaunchPreset(pass: PrerenderPassView, sources: RelaunchSources): LaunchPreset {
  const versions = versionsOf(pass, sources);
  const kept = versions === 'all' ? null : new Set(versions);
  return {
    origin: 'relaunch',
    versions,
    priority: pass.scope.priority.filter((version) => kept === null || kept.has(version)),
    locales: pass.scope.locales ?? null,
    pageTypes: pass.scope.pageTypes ?? null,
    epoch: pass.epoch < sources.servedEpoch ? sources.servedEpoch : pass.epoch,
    skipComplete: false,
    fromPass: pass.id,
  };
}
