/**
 * Points a request for the page's own origin at the SSR server itself. During SSR, relative
 * URLs (the i18n catalogues) resolve against the public origin of the page, which a container
 * cannot always reach (https://localhost:<port> in development) and should not hairpin
 * through the proxy for anyway. Any other origin, the API's included, is left alone.
 */
export function rewriteToSelfOrigin(url: string, pageOrigin: string, selfOrigin: string): string {
  let target: URL;
  try {
    target = new URL(url);
  } catch {
    return url;
  }
  return target.origin === pageOrigin ? `${selfOrigin}${target.pathname}${target.search}` : url;
}
