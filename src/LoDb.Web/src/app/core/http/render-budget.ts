/**
 * How long the API calls of one server render may wait (`serverApiTimeoutInterceptor`), set
 * by `src/server.ts` when the render starts, from `LODB_RENDER_*` of the server environment.
 */
export interface RenderBudget {
  /** Epoch milliseconds past which the render makes no more API call. */
  readonly deadline: number;
  /** Longest wait of one API call, the deadline permitting. */
  readonly callTimeoutMs: number;
}
