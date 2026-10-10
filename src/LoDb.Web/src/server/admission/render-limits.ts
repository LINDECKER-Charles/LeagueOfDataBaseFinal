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
  /** Renders of the current pages started per minute, on average, every client together. */
  readonly currentPerMinute: number;
  /** Renders of the current pages a quiet server starts at once. */
  readonly currentBurst: number;
  /** Renders one client (an IPv4 address, an IPv6 /64) has running at once, every lane. */
  readonly perClientMaxInFlight: number;
  /** Renders of a pinned version one client starts per minute, which it may also spend at once. */
  readonly pinnedPerClientPerMinute: number;
  /** Clients whose renders the admission remembers; the least recently seen is forgotten. */
  readonly trackedClients: number;
}
