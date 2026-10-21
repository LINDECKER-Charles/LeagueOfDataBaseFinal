// Starts the nginx image under test behind a stub of its upstreams, on a private Docker
// network, and removes everything afterwards. Needs the Docker CLI.
import { execFile } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { promisify } from 'node:util';
import { selfSignedCertificate } from './self-signed-certificate.mjs';

const run = promisify(execFile);
const STUB_CONF = new URL('./upstream-stub.conf', import.meta.url);
// The stack's aliases (compose.yaml, compose.override.yaml): the API, the SSR server and the
// page store all answer from the one stub. Over TLS, the store claims its alias with a
// certificate no authority signed.
const STORE_ALIAS = 'lodb-pages-store';
const UPSTREAM_ALIASES = ['lodb-api', 'lodb-web-ssr', STORE_ALIAS];
// nginx's own port (docker/nginx/Dockerfile), unprivileged.
const NGINX_PORT = 8080;
const HTTP_PORT = `${NGINX_PORT}/tcp`;
const READY_TIMEOUT_MS = 30_000;
const READY_POLL_MS = 250;
// The body of the stub's /api/large: far more than nginx's buffers and the sockets' hold, so
// that a client reading it slowly leaves the rest in nginx's temporary files.
const LARGE_ANSWER_BYTES = 8 * 1024 * 1024;
// The body of a page in the stub's page store, which keeps it compressed, as a pass does.
const STORED_PAGE = 'stored page\n';
// Where the stub's page store forwards the requests of a store that never answers
// (lib/upstream-stub.conf): a listener that reads each one and stays silent for a minute.
const SILENT_PORT = 9001;
const SILENT_SECONDS = 60;
// The stub's configuration, its stored page and its certificate travel in environment
// variables: no bind mount, whose host path syntax differs between Docker hosts. The stub is a
// plain nginx, run as root.
const STUB_COMMAND = `head -c ${LARGE_ANSWER_BYTES} /dev/zero > /tmp/large`
  + ' && printf "%s" "$STORED_PAGE" | gzip > /tmp/stored-page.gz'
  + ' && printf "%s" "$STORE_CERTIFICATE" > /tmp/store.crt'
  + ' && printf "%s" "$STORE_KEY" > /tmp/store.key'
  + ` && { nc -lk -p ${SILENT_PORT} -e sleep ${SILENT_SECONDS} & }`
  + ' && printf "%s" "$STUB_CONF" > /tmp/stub.conf'
  + ' && exec nginx -c /tmp/stub.conf -g "daemon off;"';
// The stub's log of the requests the page store got (lib/upstream-stub.conf).
const STORE_REQUESTS = '/tmp/store-requests.log';
// The image runs as compose.yaml runs it: read-only, without any capability nor a way to gain
// one, /tmp a tmpfs, each cache and the temporary files a volume that Docker seeds from the
// image (owned by `nginx`). The storage, a read-only volume of the API's on the stack, and the
// maintenance switch, a read-only folder of the host (compose.deploy.yaml), are tmpfs the checks
// write to.
const HARDENING = ['--read-only', '--cap-drop', 'ALL', '--security-opt',
  'no-new-privileges:true', '--tmpfs', '/tmp:size=64m', '--tmpfs', '/srv/storage',
  '--tmpfs', '/srv/maintenance'];
const VOLUMES = ['pages', 'archive', 'assets', 'seo', 'legacy', 'temp'];

function docker(...args) {
  return run('docker', args, { maxBuffer: 1024 * 1024 });
}

async function startStub(image, network, name) {
  const conf = await readFile(STUB_CONF, 'utf8');
  const aliases = UPSTREAM_ALIASES.flatMap((alias) => ['--network-alias', alias]);
  const { certificate, key } = selfSignedCertificate(STORE_ALIAS);
  const variables = { STUB_CONF: conf, STORED_PAGE, STORE_CERTIFICATE: certificate,
    STORE_KEY: key };
  const environment = Object.entries(variables)
    .flatMap(([variable, value]) => ['-e', `${variable}=${value}`]);
  await docker('run', '-d', '--name', name, '--network', network, ...aliases, '--user', 'root',
    ...environment, '--entrypoint', 'sh', image, '-c', STUB_COMMAND);
}

async function startNginx(image, network, name, environment) {
  const variables = Object.entries(environment)
    .flatMap(([key, value]) => ['-e', `${key}=${value}`]);
  const volumes = VOLUMES.flatMap((folder) => ['-v', `/var/cache/nginx/${folder}`]);
  await docker('run', '-d', '--name', name, '--network', network, ...HARDENING, ...volumes,
    '-p', `127.0.0.1::${NGINX_PORT}`, ...variables, image);
  const { stdout } = await docker('port', name, HTTP_PORT);
  return Number(stdout.trim().split('\n')[0].split(':').pop());
}

// nginx itself, then both upstreams once resolved, through paths that no rate limits.
const READY_PATHS = ['/healthz', '/v1/ready', '/build/ready'];

async function isAnswering(port, headers) {
  const answers = READY_PATHS.map((path) => fetch(`http://127.0.0.1:${port}${path}`, { headers })
    .then((response) => response.ok, () => false));
  return (await Promise.all(answers)).every(Boolean);
}

async function waitUntilReady(port, headers) {
  const deadline = Date.now() + READY_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (await isAnswering(port, headers)) {
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, READY_POLL_MS));
  }
  throw new Error(`nginx and its upstreams not ready within ${READY_TIMEOUT_MS} ms`);
}

/**
 * Boots the image with the given LODB_* variables; resolves to the published port, an
 * `exec` into the nginx container, `storeRequests`, the requests the stub's page store got so
 * far ("GET /bucket/key" each), `storedPage`, the body of a page it holds, and `stop`, which
 * removes the containers and network. With LODB_EDGE_SECRET set, the readiness probes send it
 * as the edge does.
 */
export async function startStack(image, environment) {
  // Unique: a test file may start several stacks.
  const prefix = `lodb-nginx-check-${process.pid}-${randomBytes(4).toString('hex')}`;
  const [network, stub, nginx] = [prefix, `${prefix}-upstream`, `${prefix}-nginx`];
  const stop = () => docker('rm', '-f', '-v', nginx, stub)
    .catch(() => undefined)
    .then(() => docker('network', 'rm', network).catch(() => undefined));
  const secret = environment.LODB_EDGE_SECRET;
  await docker('network', 'create', network);
  try {
    await startStub(image, network, stub);
    const port = await startNginx(image, network, nginx, environment);
    await waitUntilReady(port, secret ? { 'x-lodb-edge': secret } : {});
    const exec = (...command) => docker('exec', nginx, ...command);
    const storeRequests = async () => {
      const { stdout } = await docker('exec', stub, 'cat', STORE_REQUESTS);
      return stdout.split('\n').filter(Boolean);
    };
    return { port, exec, storeRequests, storedPage: STORED_PAGE, stop };
  } catch (error) {
    await stop();
    throw error;
  }
}
