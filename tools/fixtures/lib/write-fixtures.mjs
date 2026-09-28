import { existsSync, mkdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/** Hard cap of the recorded set, index included (10 MB). */
export const SIZE_BUDGET_BYTES = 10_000_000;

export const INDEX_FILE = 'index.json';

/**
 * Where a recorded body lives under the fixture directory: the URL's host and path. The
 * replay (tests/LoDb.Testing/Fixtures) derives the same path.
 */
export const bodyPath = (url) => {
  const { host, pathname } = new URL(url);
  return `${host}${pathname}`;
};

/**
 * Lays the recording out as body files plus index.json. Pure: the caller decides whether the
 * result is written.
 * @returns {{files: Map<string, Buffer>, size: number}}
 */
export function layOut(entries, { recordedOn, roles }) {
  const files = new Map();
  const responses = [...entries.values()]
    .sort((left, right) => (left.url < right.url ? -1 : left.url > right.url ? 1 : 0))
    .map((entry) => describe(entry, files));
  assertNoCaseCollision([...files.keys()]);
  const index = { recordedOn, roles, responses };
  files.set(INDEX_FILE, Buffer.from(`${JSON.stringify(index, null, 2)}\n`, 'utf8'));
  const size = [...files.values()].reduce((total, body) => total + body.length, 0);
  return { files, size };
}

/**
 * Replaces the fixture directory with the laid-out files, through a sibling staging directory:
 * a failed run leaves the previous recording untouched.
 */
export function writeFixtures(targetDir, { files, size }) {
  if (size > SIZE_BUDGET_BYTES) {
    throw new Error(`Recording weighs ${size} bytes, over the ${SIZE_BUDGET_BYTES} budget.`);
  }
  const staging = `${targetDir}.staging`;
  const previous = `${targetDir}.previous`;
  rmSync(staging, { recursive: true, force: true });
  for (const [path, body] of files) {
    const file = join(staging, path);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, body);
  }
  rmSync(previous, { recursive: true, force: true });
  if (existsSync(targetDir)) {
    renameSync(targetDir, previous);
  }
  renameSync(staging, targetDir);
  rmSync(previous, { recursive: true, force: true });
}

function describe(entry, files) {
  const { url, status, contentType, body, bodyless, reduction } = entry;
  const response = { url, status };
  if (contentType !== undefined) {
    response.contentType = contentType;
  }
  if (bodyless) {
    response.bodyless = true;
  }
  if (reduction !== undefined) {
    response.reduction = reduction;
  }
  if (body !== undefined && !bodyless) {
    files.set(bodyPath(url), body);
  }
  return response;
}

// macOS and Windows checkouts are case-insensitive: "FiddleSticks" and "Fiddlesticks" bodies
// would overwrite each other there.
function assertNoCaseCollision(paths) {
  const seen = new Map();
  for (const path of paths) {
    const folded = path.toLowerCase();
    if (seen.has(folded)) {
      throw new Error(`Bodies differ only by case: ${seen.get(folded)} and ${path}.`);
    }
    seen.set(folded, path);
  }
}
