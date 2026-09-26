const WEB_SCHEMES = ['http:', 'https:'];

/**
 * The URL a click on `target` leaves the application for: the http(s) address of the link
 * it is in, when that link goes to another origin than `pageOrigin`. Null for anything the
 * WebView keeps: the application's own pages, `mailto:` and other schemes, or no link.
 */
export function externalLinkTarget(target: EventTarget | null, pageOrigin: string): string | null {
  const link = target instanceof Element ? target.closest('a[href]') : null;
  if (!(link instanceof HTMLAnchorElement)) {
    return null;
  }
  let url: URL;
  try {
    url = new URL(link.href, pageOrigin);
  } catch {
    return null;
  }
  return WEB_SCHEMES.includes(url.protocol) && url.origin !== pageOrigin ? url.href : null;
}
