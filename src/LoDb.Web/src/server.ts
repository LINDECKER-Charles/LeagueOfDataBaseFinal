import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express, { type RequestHandler } from 'express';
import { join } from 'node:path';
import { ApiResponseMemo } from './app/core/http/api-response-memo';
import type { SsrRequestContext } from './app/core/http/ssr-request-context';
import { negotiateLocale } from './app/core/routing/locale/negotiate-locale';
import { parseAcceptLanguage } from './app/core/routing/locale/parse-accept-language';
import { CACHE_CONTROL } from './app/core/routing/response/cache-control';
import { AdmissionSummary } from './server/admission/admission-summary';
import { clientKeyOf } from './server/admission/client-key-of';
import { RenderAdmission } from './server/admission/render-admission';
import { RenderCounts } from './server/admission/render-counts';
import { FrontBuildsPublication } from './server/assets/front-builds-publication';
import { readBuildFiles } from './server/assets/read-build-files';
import { staticFolder } from './server/assets/static-folder';
import { encodedDocument } from './server/compression/encoded-document';
import { createLogger } from './server/create-logger';
import { failureHandler } from './server/failure-handler';
import { isHashedAsset } from './server/hashed-asset';
import { serveMetrics } from './server/metrics/serve-metrics';
import { SsrMetrics } from './server/metrics/ssr-metrics';
import { readServerSettings } from './server/read-server-settings';
import { requestLogging } from './server/request-logging';
import { preRouter } from './server/routing/pre-router';
import { applyServerTimeouts } from './server/security/apply-server-timeouts';
import { boundedRequests } from './server/security/bounded-requests';
import { readKnownInlineScripts } from './server/security/csp/scripts/read-known-inline-scripts';
import type { ScriptPolicy } from './server/security/csp/scripts/script-policy';
import { UnexpectedScripts } from './server/security/csp/scripts/unexpected-scripts';
import { withDocumentPolicy } from './server/security/csp/with-document-policy';
import { networkGate } from './server/security/network-gate';
import { StackPeers } from './server/security/stack-peers';

// The server reads no cookie and holds no secret: pages are the same for every visitor, so
// the proxy may cache them, and a compromised renderer has nothing to leak. It is still an
// exposed surface (ADR 0005): hosts allowed, requests and timeouts bounded, a CSP per page.

// nginx is the only peer past the network gate besides the loopback, and writes
// X-Forwarded-For itself (snippets/proxy-headers.conf): one hop names the visitor's address,
// which the render admission counts per client.
const TRUSTED_PROXY_HOPS = 1;
// The build's files a render reads (i18n catalogues, changelog), held in memory once.
const BUILD_FILE_FOLDERS = ['i18n', 'changelog'];
// What every render asks the API for with the same answer, kept for all of them.
const MEMOISED_API_PATHS = ['/api/meta'];
// The build's index templates, beside this file and in the browser's folder: the inline
// scripts they hold are the only ones a page's policy admits (src/server/security/csp).
const SERVER_TEMPLATE = 'index.server.html';
const BROWSER_TEMPLATE = 'index.csr.html';

// Under `ng serve`, the dev server mounts `reqHandler` instead of running this file.
const listening = isMainModule(import.meta.url);
const settings = readServerSettings(process.env);
const stackPeers = new StackPeers(settings.stackNetworks);
const logger = createLogger();
const browserDistFolder = join(import.meta.dirname, '../browser');
const angularApp = new AngularNodeAppEngine({
  allowedHosts: settings.allowedHosts,
  trustProxyHeaders: settings.trustProxyHeaders,
});
const requestContext: SsrRequestContext = {
  apiOrigin: settings.apiOrigin,
  // Under `ng serve`, the build lives in the dev server's memory, which serves its files.
  buildFiles: listening ? readBuildFiles(browserDistFolder, BUILD_FILE_FOLDERS) : null,
  apiResponses: new ApiResponseMemo(
    new Set(MEMOISED_API_PATHS.map((path) => `${settings.apiOrigin}${path}`)),
    settings.apiMetaTtlMs,
    () => Date.now(),
  ),
};
const renderCounts = new RenderCounts();
const renderAdmission = new RenderAdmission(settings.renderLimits, renderCounts);
const metrics = new SsrMetrics(renderCounts, renderAdmission);
const unexpectedScripts = new UnexpectedScripts(logger);
// Under `ng serve`, the dev server adds scripts of its own that no hash would admit, and holds
// the templates in its memory: its pages go without a policy.
const scriptPolicy: ScriptPolicy | null = listening
  ? {
      known: readKnownInlineScripts([
        join(import.meta.dirname, SERVER_TEMPLATE),
        join(browserDistFolder, BROWSER_TEMPLATE),
      ]),
      onUnexpected: (count) => {
        metrics.countUnexpectedScripts(count);
        unexpectedScripts.record(count);
      },
    }
  : null;
const app = express();

app.disable('x-powered-by');
app.set('trust proxy', TRUSTED_PROXY_HOPS);

// Before anything else, the request log included: the site's port answers the stack alone.
app.use(networkGate(stackPeers, () => metrics.countGateRefusal()));

// Registered before the request log: Docker polls it every few seconds.
app.get('/healthz', (_request, response) => {
  response.set('Cache-Control', 'no-store').type('text/plain').send('ok');
});

app.use(requestLogging(logger));
app.use(boundedRequests());

// `/` has no page of its own: a 302 to the locale the browser prefers (ADR 0005). The
// answer depends on Accept-Language, and a redirect only lives a minute in a shared cache.
app.get('/', (request, response) => {
  const locale = negotiateLocale(parseAcceptLanguage(request.get('Accept-Language')));
  response.set({ 'Cache-Control': CACHE_CONTROL.transient, Vary: 'Accept-Language' });
  response.redirect(302, `/${locale}/`);
});

const notFound: RequestHandler = (_request, response) => {
  response.status(404).type('text/plain').send('Not Found');
};

const staticFiles = staticFolder(browserDistFolder);
const previousBuilds =
  settings.frontBuildsFolder === null ? [] : [staticFolder(settings.frontBuildsFolder)];
const hashedAssetsOnly: RequestHandler = (request, response, next) => {
  if (isHashedAsset(request.path.slice(1))) {
    next();
  } else {
    notFound(request, response, next);
  }
};

// /build/ is reserved for the hashed bundles (deployUrl of the `web` build, ADR 0005): this
// build's own, then those the previous builds left in the shared folder, for the tabs and the
// cached pages that still name them. No page and no other file answers under it, so a stale
// or invented name is a plain 404.
app.use('/build', hashedAssetsOnly, staticFiles, ...previousBuilds, notFound);

app.use(staticFiles);

// Catalogues are static only: a missing one is a plain 404, never an SSR render. The renders
// themselves read them from memory (`buildFiles`), never over HTTP.
app.use('/i18n', notFound);

// What the URL alone decides (a 400, the static 404, a query cleaned by a redirect) costs no
// render and takes no slot.
app.use(preRouter(settings.queryLimits, () => metrics.countRedirect('query')));

// The policy hashes the inline scripts of the page as rendered, before it is compressed.
async function servedDocument(
  rendered: Response,
  request: express.Request,
  policy: ScriptPolicy,
): Promise<Response> {
  const policed = await withDocumentPolicy(rendered, policy);
  return encodedDocument(policed, request.get('Accept-Encoding'), settings.htmlGzipLevel);
}

// Pages only: the files above never wait for a slot, the renders' own catalogues included.
app.use((request, response, next) => {
  const admitted = { url: request.originalUrl, client: clientKeyOf(request.ip) };
  renderAdmission
    .run(admitted, response, async (lane) => {
      metrics.timeRender(lane, response);
      // The budget starts with the render: an API call never outlives the deadline.
      const { deadlineMs, apiCallMs } = settings.renderTimeouts;
      const renderBudget = { deadline: Date.now() + deadlineMs, callTimeoutMs: apiCallMs };
      const rendered = await angularApp.handle(request, { ...requestContext, renderBudget });
      if (rendered === null) {
        next();
        return;
      }
      metrics.countRendered(rendered.status);
      const sent =
        scriptPolicy === null ? rendered : await servedDocument(rendered, request, scriptPolicy);
      await writeResponseToNodeResponse(sent, response);
    })
    .catch(next);
});

app.use(failureHandler(logger));

if (listening) {
  const server = app.listen(settings.port, (error) => {
    if (error) {
      throw error;
    }
    logger.info({ port: settings.port }, 'http.server.listening');
  });
  applyServerTimeouts(server);
  new AdmissionSummary(renderCounts, logger).start();
  unexpectedScripts.start();
  // Never routed by nginx nor published: the host's collector reads it over its own network.
  serveMetrics(metrics.registry, settings.metricsPort, logger);
  // In the background: this build's bundles are served from its own folder meanwhile.
  if (settings.frontBuildsFolder !== null) {
    new FrontBuildsPublication(browserDistFolder, settings.frontBuildsFolder, logger).start();
  }
}

export const reqHandler = createNodeRequestHandler(app);
