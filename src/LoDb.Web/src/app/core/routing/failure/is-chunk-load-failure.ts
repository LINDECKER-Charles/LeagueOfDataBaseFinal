// What each engine says when the module script of a lazy route cannot be fetched.
const CHUNK_LOAD_MESSAGES: readonly string[] = [
  // Chromium
  'Failed to fetch dynamically imported module',
  // Firefox
  'error loading dynamically imported module',
  // WebKit
  'Importing a module script failed',
];

/**
 * Whether `error` is the browser failing to load a lazy chunk of the application: once a new
 * build is deployed, a tab opened before it may name a bundle the server no longer has.
 */
export function isChunkLoadFailure(error: unknown): boolean {
  return (
    error instanceof Error && CHUNK_LOAD_MESSAGES.some((message) => error.message.includes(message))
  );
}
