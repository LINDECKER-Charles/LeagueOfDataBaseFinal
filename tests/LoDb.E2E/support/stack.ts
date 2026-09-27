import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// The integration stack by default; a slot names its own project (docs/guides/dev-next.md).
const PROJECT = process.env['LODB_E2E_COMPOSE_PROJECT'] ?? 'lodb-next';
const ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const COMPOSE_FILES = ['compose.next.yaml', 'compose.next.override.yaml'];
// A CLI command starts a .NET host and reaches the database: seconds, not minutes.
const EXEC_TIMEOUT_MS = 120_000;

/**
 * Runs `command` in a service of the stack under test, `input` on its standard input, and
 * returns what it printed. A command that fails throws, with its output.
 */
export function execInStack(service: string, command: readonly string[], input?: string): string {
  const files = COMPOSE_FILES.flatMap((file) => ['-f', file]);
  return execFileSync(
    'docker',
    ['compose', '-p', PROJECT, ...files, 'exec', '-T', service, ...command],
    { cwd: ROOT, encoding: 'utf8', timeout: EXEC_TIMEOUT_MS, input },
  );
}
