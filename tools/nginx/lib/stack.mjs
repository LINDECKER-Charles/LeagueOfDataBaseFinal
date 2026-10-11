// Starts the nginx image under test behind a stub of its upstreams, on a private Docker
// network, and removes everything afterwards. Needs the Docker CLI.
import { execFile } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { promisify } from 'node:util';

const run = promisify(execFile);
const STUB_CONF = new URL('./upstream-stub.conf', import.meta.url);
const UPSTREAM_ALIASES = ['lodb-api', 'lodb-web-ssr'];
// nginx's own port (docker/nginx/Dockerfile), unprivileged.
const NGINX_PORT = 8080;
const HTTP_PORT = `${NGINX_PORT}/tcp`;
const READY_TIMEOUT_MS = 30_000;
const READY_POLL_MS = 250;
// The stub's configuration travels in an environment variable: no bind mount, whose host
// path syntax differs between Docker hosts. The stub is a plain nginx, run as root.
const STUB_COMMAND =
  'printf "%s" "$STUB_CONF" > /tmp/stub.conf && exec nginx -c /tmp/stub.conf -g "daemon off;"';
// The image runs as compose.yaml runs it: read-only, without any capability nor a way to gain
// one, /tmp a tmpfs, each cache a volume that Docker seeds from the image (owned by `nginx`).
// The storage, a read-only volume of the API's on the stack, is a tmpfs the checks write to.
const HARDENING = ['--read-only', '--cap-drop', 'ALL', '--security-opt',
  'no-new-privileges:true', '--tmpfs', '/tmp:size=64m', '--tmpfs', '/srv/storage'];
const CACHES = ['pages', 'archive', 'assets', 'seo', 'legacy'];

function docker(...args) {
  return run('docker', args, { maxBuffer: 1024 * 1024 });
}

async function startStub(image, network, name) {
  const conf = await readFile(STUB_CONF, 'utf8');
  const aliases = UPSTREAM_ALIASES.flatMap((alias) => ['--network-alias', alias]);
  await docker('run', '-d', '--name', name, '--network', network, ...aliases, '--user', 'root',
    '-e', `STUB_CONF=${conf}`, '--entrypoint', 'sh', image, '-c', STUB_COMMAND);
}

async function startNginx(image, network, name, environment) {
  const variables = Object.entries(environment)
    .flatMap(([key, value]) => ['-e', `${key}=${value}`]);
  const volumes = CACHES.flatMap((cache) => ['-v', `/var/cache/nginx/${cache}`]);
  await docker('run', '-d', '--name', name, '--network', network, ...HARDENING, ...volumes,
    '-p', `127.0.0.1::${NGINX_PORT}`, ...variables, image);
  const { stdout } = await docker('port', name, HTTP_PORT);
  return Number(stdout.trim().split('\n')[0].split(':').pop());
}

// nginx itself, then both upstreams once resolved, through paths that no rate limits.
const READY_PATHS = ['/healthz', '/v1/ready', '/build/ready'];

async function isAnswering(port) {
  const answers = READY_PATHS.map((path) =>
    fetch(`http://127.0.0.1:${port}${path}`).then((response) => response.ok, () => false));
  return (await Promise.all(answers)).every(Boolean);
}

async function waitUntilReady(port) {
  const deadline = Date.now() + READY_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (await isAnswering(port)) {
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, READY_POLL_MS));
  }
  throw new Error(`nginx and its upstreams not ready within ${READY_TIMEOUT_MS} ms`);
}

/**
 * Boots the image with the given LODB_* variables; resolves to the published port, an
 * `exec` into the nginx container, and `stop`, which removes the containers and network.
 */
export async function startStack(image, environment) {
  const prefix = `lodb-nginx-check-${process.pid}`;
  const [network, stub, nginx] = [prefix, `${prefix}-upstream`, `${prefix}-nginx`];
  const stop = () => docker('rm', '-f', '-v', nginx, stub)
    .catch(() => undefined)
    .then(() => docker('network', 'rm', network).catch(() => undefined));
  await docker('network', 'create', network);
  try {
    await startStub(image, network, stub);
    const port = await startNginx(image, network, nginx, environment);
    await waitUntilReady(port);
    const exec = (...command) => docker('exec', nginx, ...command);
    return { port, exec, stop };
  } catch (error) {
    await stop();
    throw error;
  }
}
