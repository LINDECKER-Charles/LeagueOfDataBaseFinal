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
}
