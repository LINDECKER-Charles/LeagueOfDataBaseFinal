// Version of an Android release (ADR 0008): the android-vX.Y.Z tag on the released commit is
// its only source, as desktop-vX.Y.Z is for the desktop. No tag, no release.

export const TAG_PREFIX = 'android-v';
// No leading zero: 2.04.0 and 2.4.0 would be two tags of one versionCode.
const RELEASE = /^(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,2})\.(0|[1-9]\d{0,2})$/;
// versionCode = X·1 000 000 + Y·1 000 + Z: ordered as the versions, readable in the Play
// Console, and below Play's ceiling up to 2100.x.y. The app compares it with latest.json.
const MAJOR_WEIGHT = 1_000_000;
const MINOR_WEIGHT = 1_000;
const MAX_VERSION_CODE = 2_100_000_000;

/** Whether the text is a release version X.Y.Z this scheme can number. */
export function isReleaseVersion(version) {
  return RELEASE.test(version);
}

/** The versionCode of X.Y.Z; throws on anything else. */
export function versionCodeOf(version) {
  const match = RELEASE.exec(version);
  if (match === null) {
    throw new Error(`${version} is not a release version X.Y.Z (minor and patch below 1000)`);
  }
  const [major, minor, patch] = match.slice(1).map(Number);
  const code = major * MAJOR_WEIGHT + minor * MINOR_WEIGHT + patch;
  if (code < 1 || code > MAX_VERSION_CODE) {
    throw new Error(`${version} gives the versionCode ${code}, outside 1..${MAX_VERSION_CODE}`);
  }
  return code;
}

/**
 * The release of a commit, from the tags that point at it: `{ release: false, reason }`
 * without an android-vX.Y.Z tag, else its version, versionCode and tag. Several fail.
 */
export function releaseOf(tags) {
  const versions = tags
    .filter((tag) => tag.startsWith(TAG_PREFIX))
    .map((tag) => tag.slice(TAG_PREFIX.length))
    .filter(isReleaseVersion);
  if (versions.length === 0) {
    return { release: false, reason: `no ${TAG_PREFIX}X.Y.Z tag` };
  }
  if (versions.length > 1) {
    throw new Error(`several release tags: ${versions.map((v) => TAG_PREFIX + v).join(', ')}`);
  }
  const [version] = versions;
  return { release: true, version, versionCode: versionCodeOf(version), tag: TAG_PREFIX + version };
}
