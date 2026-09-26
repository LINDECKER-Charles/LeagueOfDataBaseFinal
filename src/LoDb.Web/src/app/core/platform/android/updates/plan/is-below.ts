// The forms the API accepts (ClientVersion, AppVersion): a release is three numbers, and an
// app may add a SemVer pre-release suffix, such as the staging betas (1.4.0-beta.3).
const APP_VERSION = /^(\d{1,9})\.(\d{1,9})\.(\d{1,9})(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$/;
const RELEASE = /^\d{1,9}\.\d{1,9}\.\d{1,9}$/;
const PARTS = 3;

function numbersOf(version: string): number[] {
  return version.split(/[.-]/, PARTS).map(Number);
}

/**
 * Whether `version` comes before the release `floor`, as the API compares them for its 426: a
 * pre-release comes before its release. Null when either is malformed, which the caller
 * treats as "unknown", never as "older".
 */
export function isBelow(version: string, floor: string): boolean | null {
  if (!APP_VERSION.test(version) || !RELEASE.test(floor)) {
    return null;
  }
  const mine = numbersOf(version);
  const theirs = numbersOf(floor);
  for (let part = 0; part < PARTS; part++) {
    if (mine[part] !== theirs[part]) {
      return mine[part] < theirs[part];
    }
  }
  return version.includes('-');
}
