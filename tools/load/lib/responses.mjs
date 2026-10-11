// How the bench reads an answer: whether nginx's page cache served it, and how a warm-up
// stream ended.
// Plain module: imported by the k6 scripts and by node --test alike.

// X-Cache-Status (snippets/page-cache.conf): the first ones answer from the cache, the others
// waited for a render.
const FROM_CACHE = new Set(['HIT', 'STALE', 'UPDATING', 'REVALIDATED']);
const RENDERED = new Set(['MISS', 'EXPIRED', 'BYPASS']);
const FRAME_PREFIX = 'data:';
const ENDING_STAGES = new Set(['done', 'failed']);

/** `hit` when the page cache answered, `miss` when it waited for a render, else `none`. */
export function cacheClassOf(cacheStatus) {
  const status = (cacheStatus ?? '').toUpperCase();
  if (FROM_CACHE.has(status)) {
    return 'hit';
  }
  return RENDERED.has(status) ? 'miss' : 'none';
}

function lastFrameOf(body) {
  const frames = (body ?? '')
    .split('\n')
    .filter((line) => line.startsWith(FRAME_PREFIX))
    .map((line) => line.slice(FRAME_PREFIX.length).trim());
  if (frames.length === 0) {
    return null;
  }
  try {
    return JSON.parse(frames[frames.length - 1]);
  } catch {
    return null;
  }
}

/**
 * How a warm-up stream ended: `done`, `failed` (with the problem code the API gave), or `cut`
 * when it closed before its last frame, or never opened.
 */
export function warmUpEndOf(body) {
  const frame = lastFrameOf(body);
  const stage = String(frame?.stage ?? '').toLowerCase();
  if (!ENDING_STAGES.has(stage)) {
    return { end: 'cut', failure: null };
  }
  return { end: stage, failure: frame.failure ?? null };
}
