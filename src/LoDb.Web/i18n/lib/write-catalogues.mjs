import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/**
 * Replaces the content of `dir` with `files` (relative path → catalogue), as compact JSON: a
 * catalogue removed from the sources leaves no stale copy behind.
 */
export function writeCatalogues(dir, files) {
  rmSync(dir, { recursive: true, force: true });
  for (const [path, catalogue] of files) {
    const file = join(dir, path);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, JSON.stringify(catalogue));
  }
}
