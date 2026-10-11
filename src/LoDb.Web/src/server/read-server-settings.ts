import { isAbsolute } from 'node:path';
import { readRenderLimits } from './admission/read-render-limits';
import { readPositiveInteger } from './read-positive-integer';
import { readGzipLevel } from './compression/read-gzip-level';
import { readRenderTimeouts } from './rendering/read-render-timeouts';
import { readQueryLimits } from './routing/read-query-limits';
import type { ServerSettings } from './server-settings';

const DEFAULT_PORT = 4000;
// The Prometheus scrape, apart from the site's port (src/server/metrics), as on the API.
const DEFAULT_METRICS_PORT = 9464;
const MAX_PORT = 65_535;
// Enough to run the build locally; every deployed environment lists its own hosts.
const DEFAULT_ALLOWED_HOSTS = ['localhost'];
// The API's alias in the compose stack: never the short name `api`, which a shared network
// (observability) resolves to other projects' services too.
const DEFAULT_API_ORIGIN = 'http://lodb-api:8080';
const MS_PER_SECOND = 1_000;
// The API's own `Cache-Control` of /api/meta (max-age=60): a render never reads an older one
// unless the API cannot answer.
const DEFAULT_API_META_TTL_SECONDS = 60;

type Environment = Readonly<Record<string, string | undefined>>;

function listOf(value: string | undefined): string[] | undefined {
  const items = (value ?? '')
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
  return items.length > 0 ? items : undefined;
}

// A misconfigured variable stops the server at startup rather than serving on a guess.
function portOf(env: Environment, variable: string, fallback: number): number {
  const value = env[variable];
  if (value === undefined || value === '') {
    return fallback;
  }
  const port = Number(value);
  if (!Number.isInteger(port) || port < 1 || port > MAX_PORT) {
    throw new Error(`${variable} must be a TCP port, got "${value}".`);
  }
  return port;
}

function metricsPortOf(env: Environment, port: number): number {
  const metricsPort = portOf(env, 'LODB_METRICS_PORT', DEFAULT_METRICS_PORT);
  if (metricsPort === port) {
    throw new Error(`LODB_METRICS_PORT must differ from PORT, both are ${port}.`);
  }
  return metricsPort;
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

function folderOf(value: string | undefined): string | null {
  if (value === undefined || value === '') {
    return null;
  }
  if (!isAbsolute(value)) {
    throw new Error(`LODB_FRONT_BUILDS_DIR must be an absolute path, got "${value}".`);
  }
  return value;
}

/**
 * Reads PORT, LODB_METRICS_PORT, LODB_ALLOWED_HOSTS, LODB_TRUST_PROXY_HEADERS and
 * LODB_STACK_NETWORKS (comma-separated lists), LODB_API_ORIGIN, LODB_FRONT_BUILDS_DIR,
 * LODB_HTML_GZIP_LEVEL, LODB_API_META_TTL_SECONDS, the bounds of the render admission, of a
 * render's wait on the API and of a catalogue page's query.
 */
export function readServerSettings(env: Environment): ServerSettings {
  const port = portOf(env, 'PORT', DEFAULT_PORT);
  return {
    port,
    metricsPort: metricsPortOf(env, port),
    allowedHosts: listOf(env['LODB_ALLOWED_HOSTS']) ?? DEFAULT_ALLOWED_HOSTS,
    trustProxyHeaders: listOf(env['LODB_TRUST_PROXY_HEADERS']),
    stackNetworks: listOf(env['LODB_STACK_NETWORKS']) ?? [],
    apiOrigin: originOf(env['LODB_API_ORIGIN']),
    apiMetaTtlMs:
      readPositiveInteger(env, 'LODB_API_META_TTL_SECONDS', DEFAULT_API_META_TTL_SECONDS) *
      MS_PER_SECOND,
    frontBuildsFolder: folderOf(env['LODB_FRONT_BUILDS_DIR']),
    renderLimits: readRenderLimits(env),
    renderTimeouts: readRenderTimeouts(env),
    queryLimits: readQueryLimits(env),
    htmlGzipLevel: readGzipLevel(env),
  };
}
