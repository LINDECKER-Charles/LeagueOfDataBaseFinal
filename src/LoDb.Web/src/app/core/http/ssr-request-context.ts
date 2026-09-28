/**
 * What `src/server.ts` hands to every render through Angular's `REQUEST_CONTEXT`. Both values
 * come from the server environment, never from the incoming request.
 */
export interface SsrRequestContext {
  /** Origin the render calls the API on, e.g. `http://api:8080` inside compose. */
  readonly apiOrigin: string;
  /**
   * Origin of the SSR server itself, e.g. `http://127.0.0.1:4000`, or null when another
   * server hosts the handler (`ng serve`) and same-origin files are left to it.
   */
  readonly selfOrigin: string | null;
}
