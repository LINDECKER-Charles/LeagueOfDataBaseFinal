/**
 * The `count` versions listed right after `version` in `versions`, which `/api/meta` lists
 * newest first: the patches before it. None when the list does not hold `version`.
 */
export function previousVersionsOf(
  versions: readonly string[],
  version: string,
  count: number,
): string[] {
  const index = versions.indexOf(version);
  return index < 0 ? [] : versions.slice(index + 1, index + 1 + count);
}
