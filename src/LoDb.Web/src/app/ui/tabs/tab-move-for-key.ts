import type { Direction } from '@angular/cdk/bidi';
import type { TabMove } from './tab-move';

/**
 * Reads a key press on a horizontal tab list, after the ARIA tabs pattern. The arrows are
 * physical keys over a list laid out in the writing direction, so in a right-to-left page
 * the left arrow moves to the next tab. Null for any key the tab list leaves alone.
 */
export function tabMoveForKey(key: string, direction: Direction): TabMove | null {
  const forward = direction === 'rtl' ? 'ArrowLeft' : 'ArrowRight';
  const backward = direction === 'rtl' ? 'ArrowRight' : 'ArrowLeft';
  switch (key) {
    case forward:
      return 'next';
    case backward:
      return 'previous';
    case 'Home':
      return 'first';
    case 'End':
      return 'last';
    default:
      return null;
  }
}
