/**
 * The patches the version select offers: every one Data Dragon lists, and the patch the
 * build is pinned to when upstream has delisted it, so an old build stays editable on it.
 */
export function versionChoices(versions: readonly string[], pinned: string): readonly string[] {
  return pinned === '' || versions.includes(pinned) ? versions : [...versions, pinned];
}
