import type { ApkManifest } from './apk-manifest';

// Play's ceiling for a versionCode; the release workflow derives it from the version.
const MAX_VERSION_CODE = 2_100_000_000;
const RELEASE = /^\d{1,9}\.\d{1,9}\.\d{1,9}$/;
const SHA256 = /^[0-9a-f]{64}$/;
const HTTPS = 'https:';

function isVersionCode(value: unknown): value is number {
  return (
    Number.isSafeInteger(value) && (value as number) > 0 && (value as number) <= MAX_VERSION_CODE
  );
}

function isText(value: unknown, pattern: RegExp): value is string {
  return typeof value === 'string' && pattern.test(value);
}

function isApkUrl(value: unknown, apkHosts: readonly string[]): value is string {
  if (typeof value !== 'string' || !URL.canParse(value)) {
    return false;
  }
  const url = new URL(value);
  return url.protocol === HTTPS && apkHosts.includes(url.host);
}

/**
 * The manifest of the transitional channel when every field holds, otherwise null. The APK
 * must come from one of `apkHosts` over https: a manifest that points elsewhere is refused.
 */
export function parseApkManifest(body: unknown, apkHosts: readonly string[]): ApkManifest | null {
  if (typeof body !== 'object' || body === null) {
    return null;
  }
  const { versionCode, versionName, url, sha256 } = body as Record<string, unknown>;
  const holds =
    isVersionCode(versionCode) &&
    isText(versionName, RELEASE) &&
    isApkUrl(url, apkHosts) &&
    isText(sha256, SHA256);
  return holds ? { versionCode, versionName, url, sha256 } : null;
}
