/** Bounds of the render admission, read once at startup (`readRenderLimits`). */
export interface RenderLimits {
  /** Renders running at once, every lane together. */
  readonly maxInFlight: number;
  /** Renders of a pinned version running at once: the rest is kept for the current pages. */
  readonly pinnedMaxInFlight: number;
  /** Renders of a pinned version started per minute, on average. */
  readonly pinnedPerMinute: number;
  /** Renders of a pinned version a quiet server starts at once. */
  readonly pinnedBurst: number;
}
