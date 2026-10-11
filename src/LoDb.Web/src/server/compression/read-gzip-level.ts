import { constants } from 'node:zlib';
import { readPositiveInteger } from '../read-positive-integer';

type Environment = Readonly<Record<string, string | undefined>>;

const VARIABLE = 'LODB_HTML_GZIP_LEVEL';
// The level zlib picks by default: most of the size gain of level 9, for a fraction of its
// time. nginx keeps the result, so a page pays it once per render, never per visit.
const DEFAULT_LEVEL = 6;

/** Reads the gzip level of the rendered pages from `LODB_HTML_GZIP_LEVEL`, 1 to 9. */
export function readGzipLevel(env: Environment): number {
  const level = readPositiveInteger(env, VARIABLE, DEFAULT_LEVEL);
  if (level > constants.Z_BEST_COMPRESSION) {
    throw new Error(`${VARIABLE} must be a gzip level from 1 to 9, got ${level}.`);
  }
  return level;
}
