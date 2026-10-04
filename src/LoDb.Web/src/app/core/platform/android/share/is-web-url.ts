const WEB_SCHEMES = ['http:', 'https:'];

/** Whether the system browser may open `url`: http(s) only, never `intent:` or `file:`. */
export function isWebUrl(url: string): boolean {
  try {
    return WEB_SCHEMES.includes(new URL(url).protocol);
  } catch {
    return false;
  }
}
