/** How long a render may wait on the API (`readRenderTimeouts`), in milliseconds. */
export interface RenderTimeouts {
  /** From the start of a render; past it, the render makes no more API call. */
  readonly deadlineMs: number;
  /** Longest wait of one API call, the deadline permitting. */
  readonly apiCallMs: number;
}
