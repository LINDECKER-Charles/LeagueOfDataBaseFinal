import type { ManifestEntry } from '../model/manifest-entry';
import { isJsonRecord } from './is-json-record';
import { jsonRecords } from './json-records';
import { jsonText } from './json-text';

// A release id names a file next to the manifest: no path, no query can come through it.
const RELEASE_ID = /^[\w.-]+$/;

/**
 * The entries of `changelog/manifest.json`, in the manifest's order (newest first). It never
 * throws: a missing or corrupt manifest reads as an empty history, and an entry without a
 * usable id is skipped.
 */
export function parseManifest(json: unknown): readonly ManifestEntry[] {
  const patches = isJsonRecord(json) ? jsonRecords(json['patches']) : [];
  return patches.flatMap((patch) => {
    const id = jsonText(patch['id']);
    return id !== null && RELEASE_ID.test(id) ? [{ id, version: jsonText(patch['version']) }] : [];
  });
}
