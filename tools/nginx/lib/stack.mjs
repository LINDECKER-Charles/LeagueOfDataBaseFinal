// Starts the nginx image under test behind a stub of its upstreams, on a private Docker
// network, and removes everything afterwards. Needs the Docker CLI.
import { execFile } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { promisify } from 'node:util';

const run = promisify(execFile);
const STUB_CONF = new URL('./upstream-stub.conf', import.meta.url);
const UPSTREAM_ALIASES = ['lodb-api', 'lodb-web-ssr'];
const HTTP_PORT = '80/tcp';
const READY_TIMEOUT_MS = 30_000;
const READY_POLL_MS = 250;
// The stub's configuration travels in an environment variable: no bind mount, whose host
// path syntax differs between Docker hosts.
const STUB_COMMAND =
  'printf "%s" "$STUB_CONF" > /tmp/stub.conf && exec nginx -c /tmp/stub.conf -g "daemon off;"';

function docker(...args) {
  return run('docker', args, { maxBuffer: 1024 * 1024 });
}

async function startStub(image, network, name) {
  const conf = await readFile(STUB_CONF, 'utf8');
  const aliases = UPSTREAM_ALIASES.flatMap((alias) => ['--network-alias', alias]);
  await docker('run', '-d', '--name', name, '--network', network, ...aliases,
    '-e', `STUB_CONF=${conf}`, '--entrypoint', 'sh', image, '-c', STUB_COMMAND);
}

async function startNginx(image, network, name, environment) {
  const variables = Object.entries(environment)
    .flatMap(([key, value]) => ['-e', `${key}=${value}`]);
  await docker('run', '-d', '--name', name, '--network', network,
    '-p', '127.0.0.1::80', ...variables, image);
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
  const stop = () => docker('rm', '-f', nginx, stub)
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
