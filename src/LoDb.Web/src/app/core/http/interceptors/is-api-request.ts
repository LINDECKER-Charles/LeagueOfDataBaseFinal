/**
 * Whether a request goes to the API (`{apiOrigin}/api/…`): the only requests that may carry
 * the application's headers. Catalogues, Data Dragon images and any third party never do.
 * An empty origin, as during the build-time route extraction, matches relative API paths.
 */
export function isApiRequest(url: string, apiOrigin: string): boolean {
  return url.startsWith(`${apiOrigin}/api/`);
}
