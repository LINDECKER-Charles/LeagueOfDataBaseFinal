import type { PublicUrl } from './public-url';

/** Writes a URL cut by `parsePublicUrl` back as a root-relative URL. */
export function formatPublicUrl(url: PublicUrl): string {
  const segments = [url.locale, url.version, ...url.page].filter(
    (segment): segment is string => segment !== null,
  );
  const slash = url.trailingSlash && segments.length > 0 ? '/' : '';
  return `/${segments.join('/')}${slash}${url.query.toString()}${url.fragment}`;
}
