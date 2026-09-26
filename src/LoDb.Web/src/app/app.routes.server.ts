import { RenderMode, type ServerRoute } from '@angular/ssr';

// Every page renders per request until L3.1 applies the render modes of ADR 0005.
export const serverRoutes: ServerRoute[] = [{ path: '**', renderMode: RenderMode.Server }];
