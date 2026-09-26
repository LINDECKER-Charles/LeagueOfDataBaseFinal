import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join, relative } from 'node:path';
import type { SsrRequestContext } from './app/core/http/ssr-request-context';
import { negotiateLocale } from './app/core/routing/locale/negotiate-locale';
import { parseAcceptLanguage } from './app/core/routing/locale/parse-accept-language';
import { CACHE_CONTROL } from './app/core/routing/response/cache-control';
import { createLogger } from './server/create-logger';
import { failureHandler } from './server/failure-handler';
import { readServerSettings } from './server/read-server-settings';
import { requestLogging } from './server/request-logging';
import { staticCacheControl } from './server/static-cache-control';

// The server reads no cookie and holds no secret: pages are the same for every visitor, so
// the proxy may cache them, and a compromised renderer has nothing to leak.

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

// `/` has no page of its own: a 302 to the locale the browser prefers (ADR 0005). The
// answer depends on Accept-Language, and a redirect only lives a minute in a shared cache.
app.get('/', (request, response) => {
  const locale = negotiateLocale(parseAcceptLanguage(request.get('Accept-Language')));
  response.set({ 'Cache-Control': CACHE_CONTROL.transient, Vary: 'Accept-Language' });
  response.redirect(302, `/${locale}/`);
});

app.use(
  express.static(browserDistFolder, {
    index: false,
    redirect: false,
    cacheControl: false,
    setHeaders: (response, filePath) => {
      const cacheControl = staticCacheControl(relative(browserDistFolder, filePath));
      response.setHeader('Cache-Control', cacheControl);
    },
  }),
);

// Catalogues (public/i18n) are static only: a missing one is a plain 404, never an SSR render.
// The renderer fetches them from its own address, which the render's Host check would refuse.
app.use('/i18n', (_request, response) => {
  response.status(404).type('text/plain').send('Not Found');
});

app.use((request, response, next) => {
  angularApp
    .handle(request, requestContext)
    .then((rendered) => (rendered ? writeResponseToNodeResponse(rendered, response) : next()))
    .catch(next);
});

app.use(failureHandler(logger));

if (listening) {
  app.listen(settings.port, (error) => {
    if (error) {
      throw error;
    }
    logger.info({ port: settings.port }, 'http.server.listening');
  });
}

export const reqHandler = createNodeRequestHandler(app);
