/** The anchor of a release in the timeline: `v2-2-1` for 2.2.1, as on the current site. */
export function releaseAnchor(version: string): string {
  return `v${version.replace(/\./g, '-')}`;
}
