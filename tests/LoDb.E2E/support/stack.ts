import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// The integration stack by default; a slot names its own project (docs/guides/developpement.md).
const PROJECT = process.env['LODB_E2E_COMPOSE_PROJECT'] ?? 'lodb-dev';
const ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const COMPOSE_FILES = ['compose.yaml', 'compose.override.yaml'];
// A CLI command starts a .NET host and reaches the database: seconds, not minutes.
const EXEC_TIMEOUT_MS = 120_000;

/** What a command of the stack reads, and how long it may take. */
export interface StackCommandOptions {
  /** Written to its standard input. */
  readonly input?: string;
  /** Beyond it, the command is killed and throws; two minutes by default. */
  readonly timeoutMs?: number;
}

/**
 * Runs `command` in a service of the stack under test and returns what it printed. A command
 * that fails, or outlasts its timeout, throws, with its output.
 */
export function execInStack(
  service: string,
  command: readonly string[],
  { input, timeoutMs = EXEC_TIMEOUT_MS }: StackCommandOptions = {},
): string {
  const files = COMPOSE_FILES.flatMap((file) => ['-f', file]);
  return execFileSync(
    'docker',
    ['compose', '-p', PROJECT, ...files, 'exec', '-T', service, ...command],
    { cwd: ROOT, encoding: 'utf8', timeout: timeoutMs, input },
  );
}
