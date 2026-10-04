// Child processes of the collection: docker only, run from the repository root.
import { spawnSync } from 'node:child_process';
import { repoRoot } from './settings.mjs';

const maxBuffer = 512 * 1024 * 1024;

/**
 * Runs a command and returns its stdout as a Buffer; throws with its stderr on failure so
 * that a collection never goes on with a half-written run.
 */
export function run(command, args, { input, allowFailure = false } = {}) {
  const result = spawnSync(command, args, { cwd: repoRoot, input, maxBuffer });
  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0 && !allowFailure) {
    const stderr = result.stderr.toString().trim().split('\n').slice(-20).join('\n');
    throw new Error(`${command} ${args.join(' ')} exited with ${result.status}\n${stderr}`);
  }
  return result.stdout;
}

/** Same as {@link run}, stdout and stderr shown as they come: for the long writers. */
export function stream(command, args) {
  const result = spawnSync(command, args, { cwd: repoRoot, stdio: 'inherit' });
  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(' ')} exited with ${result.status}`);
  }
}

export function docker(args, options) {
  return run('docker', args, options);
}
