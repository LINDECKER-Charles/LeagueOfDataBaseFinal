const SEGMENT_SEPARATOR = '.';

/** Versions sharing a first segment, in the order they were listed. */
export interface VersionGroup {
  readonly major: string;
  readonly versions: readonly string[];
}

/**
 * The versions grouped by their first segment (`16.19.1` under `16`), groups and versions in
 * the order Data Dragon lists them, newest first. A version listed out of order joins the
 * group its first segment already opened, so each major number has a single group.
 */
export function versionGroupsOf(versions: readonly string[]): VersionGroup[] {
  const groups = new Map<string, string[]>();
  for (const version of versions) {
    const major = version.split(SEGMENT_SEPARATOR, 1)[0];
    const group = groups.get(major);
    if (group === undefined) {
      groups.set(major, [version]);
    } else {
      group.push(version);
    }
  }
  return Array.from(groups, ([major, members]) => ({ major, versions: members }));
}
