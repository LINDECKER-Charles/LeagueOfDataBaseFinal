import type { CacheSlot } from './cache-slot';
import { trimCache } from './trim-cache';

// A page the server marks private or unstorable must not outlive the response.
const UNSTORABLE = /\b(?:no-store|private)\b/i;

function isStorable(response: Response): boolean {
  const cacheControl = response.headers.get('Cache-Control') ?? '';
  return response.ok && response.type === 'basic' && !UNSTORABLE.test(cacheControl);
}

/**
 * Keeps a copy of a successful same-origin response. The copy is cloned before the first
 * await, while the body is still unread: the caller hands the original to the page.
 */
export async function storeCopy(
  slot: CacheSlot | undefined,
  request: Request,
  response: Response,
): Promise<void> {
  if (slot === undefined || !isStorable(response)) {
    return;
  }
  const copy = response.clone();
  await slot.cache.put(request, copy);
  await trimCache(slot.cache, slot.limit);
}
