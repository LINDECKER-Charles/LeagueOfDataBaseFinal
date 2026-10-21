import { duration } from '../../../../../format/duration';
import type { AdminText } from '../../../../../shared/inject-admin-text';

const TIME = 'prerender.common.time';
const SECOND_MS = 1_000;

/**
 * An instant relative to `now`, both in milliseconds: « il y a 4 s », « dans 18 s », and
 * « à l'instant » within a second either way.
 */
export function relativeTime(at: number, now: number, texts: AdminText): string {
  const gap = at - now;
  if (Math.abs(gap) < SECOND_MS) {
    return texts.text(`${TIME}.now`);
  }
  const length = duration(Math.abs(gap) / SECOND_MS);
  return texts.text(`${TIME}.${gap < 0 ? 'ago' : 'in'}`, { duration: length });
}
