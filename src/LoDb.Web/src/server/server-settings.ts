import type { RenderLimits } from './admission/render-limits';
import type { RenderTimeouts } from './rendering/render-timeouts';
import type { QueryLimits } from './routing/query-limits';

/** Configuration of the SSR server, read once from its environment at startup. */
export interface ServerSettings {
  readonly port: number;
  /** Hosts Angular renders for; any other Host header gets a 400 (SSRF protection). */
  readonly allowedHosts: readonly string[];
  /** Proxy headers Angular may read, or undefined to fall back to NG_TRUST_PROXY_HEADERS. */
  readonly trustProxyHeaders: readonly string[] | undefined;
  readonly apiOrigin: string;
  /** Loopback origin of this very server, or null when another server hosts the handler. */
  readonly selfOrigin: string | null;
  /**
   * Folder shared by the successive builds (volume `front-builds`), where this one publishes
   * its hashed bundles and /build/ finds those of the previous ones; null serves this
   * build's alone.
   */
  readonly frontBuildsFolder: string | null;
  /** Bounds of the render admission (`RenderAdmission`). */
  readonly renderLimits: RenderLimits;
  /** How long a render may wait on the API (`RenderBudget` of each render). */
  readonly renderTimeouts: RenderTimeouts;
  /** Bounds of the query a catalogue page keeps (`preRouter`). */
  readonly queryLimits: QueryLimits;
}
