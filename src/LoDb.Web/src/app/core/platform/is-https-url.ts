const HTTPS = 'https:';

/**
 * Whether `url` may be opened outside the application: an absolute https address, never a
 * cleartext one (readable and alterable on the way), a script, a file or an app's scheme.
 * `new URL()` rather than `URL.parse()`, which Safari only ships since version 18.
 */
export function isHttpsUrl(url: string): boolean {
  try {
    return new URL(url).protocol === HTTPS;
  } catch {
    return false;
  }
}
