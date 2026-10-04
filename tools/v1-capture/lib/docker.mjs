// Thin synchronous wrapper around the docker CLI: the capture is sequential by design
// (a single database and a single API process), so blocking calls keep it readable.
import { spawnSync } from 'node:child_process';

const maxOutputBytes = 64 * 1024 * 1024;

/**
 * Runs `docker <args>` and returns the completed process.
 * @param {string[]} args
 * @param {{ input?: string, allowFailure?: boolean }} [options]
 */
export function docker(args, options = {}) {
  const result = spawnSync('docker', args, {
    encoding: 'utf8',
    input: options.input,
    maxBuffer: maxOutputBytes,
  });
  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0 && !options.allowFailure) {
    const command = args.slice(0, 3).join(' ');
    throw new Error(`docker ${command} failed (${result.status}): ${result.stderr.trim()}`);
  }
  return result;
}

/** Removes a container if it exists; a missing container is not an error. */
export function removeContainer(name) {
  docker(['rm', '--force', '--volumes', name], { allowFailure: true });
}

/** Reports whether the named container exists and is running. */
export function isRunning(name) {
  const result = docker(['inspect', '--format', '{{.State.Running}}', name], {
    allowFailure: true,
  });
  return result.status === 0 && result.stdout.trim() === 'true';
}

/** Creates the named network unless it already exists. */
export function ensureNetwork(name, label) {
  const existing = docker(['network', 'inspect', name], { allowFailure: true });
  if (existing.status !== 0) {
    docker(['network', 'create', '--label', label, name]);
  }
}

/** Removes the named network; a missing network is not an error. */
export function removeNetwork(name) {
  docker(['network', 'rm', name], { allowFailure: true });
}
