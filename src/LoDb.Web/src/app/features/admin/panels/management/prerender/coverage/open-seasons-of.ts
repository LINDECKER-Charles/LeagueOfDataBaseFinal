import type { CoverageGroup } from './coverage-groups';

// Enough rows for a screen or two of gaps: past them, a season with gaps waits for a click.
const OPEN_ROWS = 60;

/**
 * The seasons the coverage table opens by itself (§ 5.2): those with a gap, newest first, as
 * long as their rows stay within `limit`; the newest with a gap always, however long. An
 * epoch with few gaps opens them all; one never written opens its newest season, not its
 * 400 patches. The others stay folded, one click away.
 */
export function openSeasonsOf(
  groups: readonly CoverageGroup[],
  limit = OPEN_ROWS,
): ReadonlySet<string> {
  const open = new Set<string>();
  let rows = 0;
  for (const group of groups.filter((season) => season.missing > 0)) {
    if (open.size > 0 && rows + group.rows.length > limit) {
      break;
    }
    open.add(group.key);
    rows += group.rows.length;
  }
  return open;
}
