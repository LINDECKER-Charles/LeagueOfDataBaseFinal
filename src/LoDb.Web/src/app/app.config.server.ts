import { type ApplicationConfig, mergeApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { provideSsrHttp } from './core/http/provide-ssr-http';

// Merged after appConfig: its API_BASE_URL and HttpBackend override the browser ones.
const serverConfig: ApplicationConfig = {
  providers: [provideServerRendering(withRoutes(serverRoutes)), provideSsrHttp()],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
