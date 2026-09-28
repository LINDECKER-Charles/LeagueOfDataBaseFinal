// A path starting with two slashes, or a slash and a backslash, reads as another host once
// written back as a relative URL.
const HOST_RELATIVE = /^\/[/\\]/;

/**
 * Where to go back after signing in or completing an action: `candidate` when it stays on
 * `origin` (scheme, host and port of the page), `fallback` otherwise. A return URL comes
 * from the query string, so anyone can forge it: an off-site target would turn the sign-in
 * page into an open redirect that leaks the visitor to another site. The API applies the
 * same rule (L4.2). The result is root-relative: path, query and fragment.
 */
export function safeReturnUrl(
  candidate: string | null | undefined,
  origin: string,
  fallback: string,
): string {
  if (!candidate) {
    return fallback;
  }
  let target: URL;
  try {
    target = new URL(candidate, origin);
  } catch {
    return fallback;
  }
  const path = `${target.pathname}${target.search}${target.hash}`;
  return target.origin === new URL(origin).origin && !HOST_RELATIVE.test(path) ? path : fallback;
}
