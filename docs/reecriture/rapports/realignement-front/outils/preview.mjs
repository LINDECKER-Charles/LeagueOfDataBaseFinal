// Local preview of a built SSR front (npm run build:web), served like the lodb-next nginx does.
// Usage: node preview.mjs <path to src/LoDb.Web of a checkout or worktree> <port>
//   -> front on http://localhost:<port>/en/ ; SSR renderer on <port+1> (internal).
// Pages come from the given build; /api, /cdn, /v1, /webhooks, sitemaps, robots and llms go
// to the lodb-next stack (nginx on :18080), so data and images are the integration stack's.
import http from 'node:http';
import { spawn } from 'node:child_process';
import path from 'node:path';

const [, , webRoot, portArg] = process.argv;
const port = Number(portArg ?? 4300);
const ssrPort = port + 1;
const STACK = { host: 'localhost', port: 18080 };
const TO_STACK = /^\/(api|cdn|v1|webhooks|sitemaps?|sitemap\.xml|robots\.txt|llms\.txt)(\/|$|\?|\.)/;

const ssr = spawn(process.execPath, [path.join(webRoot, 'dist/web/server/server.mjs')], {
  env: {
    ...process.env,
    PORT: String(ssrPort),
    LODB_API_ORIGIN: 'http://localhost:18081',
    LODB_ALLOWED_HOSTS: 'localhost,127.0.0.1',
    LODB_TRUST_PROXY_HEADERS: 'x-forwarded-host,x-forwarded-proto',
    NODE_ENV: 'production',
  },
  stdio: ['ignore', 'ignore', 'inherit'],
});
process.on('exit', () => ssr.kill());
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => process.exit(0));

http
  .createServer((req, res) => {
    const toStack = TO_STACK.test(req.url ?? '/');
    const target = toStack ? STACK : { host: '127.0.0.1', port: ssrPort };
    const headers = { ...req.headers };
    if (toStack) {
      // The API checks Origin on unsafe requests against its own site origin.
      if (headers.origin) headers.origin = 'http://localhost:18080';
      headers.host = 'localhost:18080';
    } else {
      headers['x-forwarded-host'] = req.headers.host;
      headers['x-forwarded-proto'] = 'http';
    }
    const upstream = http.request(
      { ...target, method: req.method, path: req.url, headers },
      (up) => {
        res.writeHead(up.statusCode ?? 502, up.headers);
        up.pipe(res);
      },
    );
    upstream.on('error', (e) => {
      res.writeHead(502, { 'content-type': 'text/plain' });
      res.end(`preview upstream error: ${e.message}`);
    });
    req.pipe(upstream);
  })
  .listen(port, () => console.log(`preview on http://localhost:${port}/en/ (ssr ${ssrPort})`));
