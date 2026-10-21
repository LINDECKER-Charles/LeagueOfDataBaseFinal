import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';

/** The versions of a pasted list, sorted by what the dialog may do with them. */
export interface PasteResult {
  /** Older patches of the report, in the pasted order: ticked and rendered first. */
  readonly known: readonly string[];
  /** The latest patch, which the SSR serves and no pass renders. */
  readonly latest: readonly string[];
  /** Anything else: not an older patch. */
  readonly unknown: readonly string[];
  /** Whether more than 100 are known: the ones past it are ticked without priority. */
  readonly isCapped: boolean;
}

// PassRequestReader.MaxPriorityVersions: the API refuses more.
const MAX_PRIORITY = 100;

/**
 * Sorts the versions of a pasted list (§ 3.3.7), from `versionTokens`: the older patches
 * the report knows, the latest patch, and the rest, each in the pasted order.
 */
export function pasteResultOf(
  tokens: readonly string[],
  report: PrerenderPatchesReport,
): PasteResult {
  const older = new Set(report.patches.map((row) => row.version));
  const known = tokens.filter((token) => older.has(token));
  const latest = tokens.filter((token) => token === report.latestVersion);
  const unknown = tokens.filter((token) => !older.has(token) && token !== report.latestVersion);
  return { known, latest, unknown, isCapped: known.length > MAX_PRIORITY };
}
