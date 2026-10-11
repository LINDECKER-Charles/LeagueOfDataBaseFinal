import type { ApiResponseMemo } from './api-response-memo';
import type { BuildFiles } from './build-files';
import type { RenderBudget } from './render-budget';

/**
 * What `src/server.ts` hands to every render through Angular's `REQUEST_CONTEXT`. Every value
 * comes from the server environment or the process, never from the incoming request.
 */
export interface SsrRequestContext {
  /** Origin the render calls the API on, e.g. `http://lodb-api:8080` inside compose. */
  readonly apiOrigin: string;
  /**
   * The build's JSON files, read once by the server (`SsrHttpBackend` answers the page's own
   * files from them), or null when another server hosts the handler (`ng serve`) and serves
   * them itself.
   */
  readonly buildFiles: BuildFiles | null;
  /** The process's memo of the API reads every render repeats (`/api/meta`). */
  readonly apiResponses: ApiResponseMemo;
  /** Bounds of this render's API calls; absent, each call keeps the default bound. */
  readonly renderBudget?: RenderBudget;
}
