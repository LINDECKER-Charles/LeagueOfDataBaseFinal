import type { ServerSettings } from './server-settings';

const DEFAULT_PORT = 4000;
const MAX_PORT = 65_535;
// Enough to run the build locally; every deployed environment lists its own hosts.
const DEFAULT_ALLOWED_HOSTS = ['localhost'];
// The API service of the compose stack (plan, section 5.2).
const DEFAULT_API_ORIGIN = 'http://api:8080';
const LOOPBACK_HOST = '127.0.0.1';

type Environment = Readonly<Record<string, string | undefined>>;

function listOf(value: string | undefined): string[] | undefined {
  const items = (value ?? '')
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
  return items.length > 0 ? items : undefined;
}

// A misconfigured variable stops the server at startup rather than serving on a guess.
function portOf(value: string | undefined): number {
  if (value === undefined || value === '') {
    return DEFAULT_PORT;
  }
  const port = Number(value);
  if (!Number.isInteger(port) || port < 1 || port > MAX_PORT) {
    throw new Error(`PORT must be a TCP port, got "${value}".`);
  }
  return port;
}

function originOf(value: string | undefined): string {
  if (value === undefined || value === '') {
    return DEFAULT_API_ORIGIN;
  }
  const url = new URL(value);
  if (url.pathname !== '/' || url.search !== '' || url.hash !== '') {
    throw new Error(`LODB_API_ORIGIN must be an origin without path, got "${value}".`);
  }
  return url.origin;
}

/**
 * Reads PORT, LODB_ALLOWED_HOSTS and LODB_TRUST_PROXY_HEADERS (comma-separated lists) and
 * LODB_API_ORIGIN. `listening` tells whether this process serves HTTP itself, which is what
 * makes its loopback origin usable by the renders.
 */
export function readServerSettings(env: Environment, listening: boolean): ServerSettings {
  const port = portOf(env['PORT']);
  return {
    port,
    allowedHosts: listOf(env['LODB_ALLOWED_HOSTS']) ?? DEFAULT_ALLOWED_HOSTS,
    trustProxyHeaders: listOf(env['LODB_TRUST_PROXY_HEADERS']),
    apiOrigin: originOf(env['LODB_API_ORIGIN']),
    selfOrigin: listening ? `http://${LOOPBACK_HOST}:${port}` : null,
  };
}
