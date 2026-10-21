/**
 * What an epoch holds of an older patch, over its locales: each `admin.prerender.common.
 * status.*` key, and the tone of its badge.
 */
export type PatchStatus = 'complete' | 'complete_with_failures' | 'partial' | 'absent' | 'failed';

// The coverage codes of a cell (PrerenderPatchRow.cells): c complete, w complete with failed
// pages, p partial, f failed for good, - absent.
const COMPLETE = 'c';
const WITH_FAILURES = 'w';
const FAILED = 'f';
const ABSENT = '-';

function every(cells: string, codes: string): boolean {
  return [...cells].every((code) => codes.includes(code));
}

/**
 * The status of a patch from its cells: complete when each locale is `c`, complete with
 * failures when each is `c` or `w`, failed when none is but one failed for good, absent when
 * none holds anything, partial otherwise.
 */
export function patchStatusOf(row: { readonly cells: string }): PatchStatus {
  const { cells } = row;
  if (cells === '' || every(cells, ABSENT)) {
    return 'absent';
  }
  if (every(cells, COMPLETE)) {
    return 'complete';
  }
  if (every(cells, COMPLETE + WITH_FAILURES)) {
    return 'complete_with_failures';
  }
  const holdsSome = cells.includes(COMPLETE) || cells.includes(WITH_FAILURES);
  return !holdsSome && cells.includes(FAILED) ? 'failed' : 'partial';
}
