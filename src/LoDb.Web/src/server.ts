import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express, { type RequestHandler } from 'express';
import { join, relative } from 'node:path';
import type { SsrRequestContext } from './app/core/http/ssr-request-context';
import { negotiateLocale } from './app/core/routing/locale/negotiate-locale';
import { parseAcceptLanguage } from './app/core/routing/locale/parse-accept-language';
import { CACHE_CONTROL } from './app/core/routing/response/cache-control';
import { createLogger } from './server/create-logger';
import { failureHandler } from './server/failure-handler';
import { isHashedAsset } from './server/hashed-asset';
import { readServerSettings } from './server/read-server-settings';
import { requestLogging } from './server/request-logging';
import { applyServerTimeouts } from './server/security/apply-server-timeouts';
import { boundedRequests } from './server/security/bounded-requests';
import { documentPolicy } from './server/security/document-policy';
import { withDocumentPolicy } from './server/security/with-document-policy';
import { staticCacheControl } from './server/static-cache-control';

// The server reads no cookie and holds no secret: pages are the same for every visitor, so
// the proxy may cache them, and a compromised renderer has nothing to leak. It is still an
// exposed surface (ADR 0005): hosts allowed, requests and timeouts bounded, a CSP per page.

// Under `ng serve`, the dev server mounts `reqHandler` instead of running this file.
const listening = isMainModule(import.meta.url);
const settings = readServerSettings(process.env, listening);
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
const app = express();

app.disable('x-powered-by');

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

const staticFiles = express.static(browserDistFolder, {
  index: false,
  redirect: false,
  cacheControl: false,
  setHeaders: (response, filePath) => {
    response.setHeader('Cache-Control', staticCacheControl(relative(browserDistFolder, filePath)));
    // The offline page: a document of its own, with no script at all.
    if (filePath.endsWith('.html')) {
      response.setHeader('Content-Security-Policy', documentPolicy([]));
    }
  },
});

// /build/ is reserved for the hashed bundles (deployUrl of the `web` build, ADR 0005): no page
// and no other file answers under it, so a stale or invented name is a plain 404.
app.use('/build', (request, response, next) => {
  if (isHashedAsset(request.path.slice(1))) {
    staticFiles(request, response, next);
  } else {
    next();
  }
});
app.use('/build', notFound);

app.use(staticFiles);

// Catalogues (public/i18n) are static only: a missing one is a plain 404, never an SSR render.
// The renderer fetches them from its own address, which the render's Host check would refuse.
app.use('/i18n', notFound);

app.use((request, response, next) => {
  angularApp
    .handle(request, requestContext)
    .then(async (rendered) => {
      if (rendered === null) {
        next();
        return;
      }
      // Under `ng serve`, the dev server adds scripts of its own that no hash would admit.
      const sent = listening ? await withDocumentPolicy(rendered) : rendered;
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
}

export const reqHandler = createNodeRequestHandler(app);
