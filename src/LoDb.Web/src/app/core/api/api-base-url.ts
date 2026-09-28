import { InjectionToken } from '@angular/core';

/**
 * Origin of the API, without path: the generated client (L2.2) appends `/api/...` itself.
 * In the browser it comes from the platform (page origin on the web and desktop, public
 * origin on Android); on the server from `provideSsrHttp` (internal origin, never the public
 * one, so SSR calls do not leave the Docker network).
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
