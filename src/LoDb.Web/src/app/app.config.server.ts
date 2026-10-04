import { type ApplicationConfig, ErrorHandler, mergeApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { createLogger } from '../server/create-logger';
import { SsrErrorHandler } from '../server/errors/ssr-error-handler';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { provideSsrHttp } from './core/http/provide-ssr-http';

// Merged after appConfig: its API_BASE_URL and HttpBackend override the browser ones.
// The error handler is built once per process, not per render: a logger holds a stdout stream.
const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    provideSsrHttp(),
    { provide: ErrorHandler, useValue: new SsrErrorHandler(createLogger()) },
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
