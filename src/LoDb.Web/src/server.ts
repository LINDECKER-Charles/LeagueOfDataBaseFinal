import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express, { type RequestHandler } from 'express';
import { join } from 'node:path';
import type { SsrRequestContext } from './app/core/http/ssr-request-context';
import { negotiateLocale } from './app/core/routing/locale/negotiate-locale';
import { parseAcceptLanguage } from './app/core/routing/locale/parse-accept-language';
import { CACHE_CONTROL } from './app/core/routing/response/cache-control';
import { AdmissionSummary } from './server/admission/admission-summary';
import { clientKeyOf } from './server/admission/client-key-of';
import { RenderAdmission } from './server/admission/render-admission';
import { RenderCounts } from './server/admission/render-counts';
import { FrontBuildsPublication } from './server/assets/front-builds-publication';
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
import { networkGate } from './server/security/network-gate';
import { StackPeers } from './server/security/stack-peers';
import { withDocumentPolicy } from './server/security/with-document-policy';

// The server reads no cookie and holds no secret: pages are the same for every visitor, so
// the proxy may cache them, and a compromised renderer has nothing to leak. It is still an
// exposed surface (ADR 0005): hosts allowed, requests and timeouts bounded, a CSP per page.

// nginx is the only peer past the network gate besides the loopback, and writes
// X-Forwarded-For itself (snippets/proxy-headers.conf): one hop names the visitor's address,
// which the render admission counts per client.
const TRUSTED_PROXY_HOPS = 1;

// Under `ng serve`, the dev server mounts `reqHandler` instead of running this file.
const listening = isMainModule(import.meta.url);
const settings = readServerSettings(process.env, listening);
const stackPeers = new StackPeers(settings.stackNetworks);
const logger = createLogger();
const browserDistFolder = join(import.meta.dirname, '../browser');
const angularApp = new AngularNodeAppEngine({
  allowedHosts: settings.allowedHosts,
  trustProxyHeaders: settings.trustProxyHeaders,
});
const requestContext: SsrRequestContext = {
  apiOrigin: settings.apiOrigin,
  selfOrigin: settings.selfOrigin,
};
const renderCounts = new RenderCounts();
const renderAdmission = new RenderAdmission(settings.renderLimits, renderCounts);
const metrics = new SsrMetrics(renderCounts, renderAdmission);
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

// Catalogues (public/i18n) are static only: a missing one is a plain 404, never an SSR render.
// The renderer fetches them from its own address, which the render's Host check would refuse.
app.use('/i18n', notFound);

// What the URL alone decides (a 400, the static 404, a query cleaned by a redirect) costs no
// render and takes no slot.
app.use(preRouter(settings.queryLimits, () => metrics.countRedirect('query')));

// The policy hashes the inline scripts of the page as rendered, before it is compressed.
async function servedDocument(rendered: Response, request: express.Request): Promise<Response> {
  const policed = await withDocumentPolicy(rendered);
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
      // Under `ng serve`, the dev server adds scripts of its own that no hash would admit.
      const sent = listening ? await servedDocument(rendered, request) : rendered;
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
  // Never routed by nginx nor published: the host's collector reads it over its own network.
  serveMetrics(metrics.registry, settings.metricsPort, logger);
  // In the background: this build's bundles are served from its own folder meanwhile.
  if (settings.frontBuildsFolder !== null) {
    new FrontBuildsPublication(browserDistFolder, settings.frontBuildsFolder, logger).start();
  }
}

export const reqHandler = createNodeRequestHandler(app);
