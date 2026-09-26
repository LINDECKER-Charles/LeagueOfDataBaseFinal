import { spawnSync } from 'node:child_process';
import { relative } from 'node:path';
import { API_PATHS } from './api-paths.mjs';

/**
 * Files of the contract that differ from the committed ones: modified, deleted, and new
 * files too, which `git diff --exit-code` alone would not see.
 */
export function findDrift(paths = API_PATHS) {
  const scope = [paths.openApiDir, paths.clientDir].map((dir) => relative(paths.repoRoot, dir));
  const result = spawnSync(
    'git',
    ['status', '--porcelain=v1', '--untracked-files=all', '--', ...scope],
    { cwd: paths.repoRoot, encoding: 'utf8' },
  );
  if (result.status !== 0) {
    throw new Error(`git status failed: ${result.error?.message ?? result.stderr}`);
  }
  return parsePorcelain(result.stdout);
}

/** Reads `git status --porcelain=v1` lines as `{ status, path }` entries. */
export function parsePorcelain(output) {
  return output
    .split('\n')
    .filter((line) => line.length > 3)
    .map((line) => ({ status: line.slice(0, 2).trim(), path: line.slice(3) }));
}
