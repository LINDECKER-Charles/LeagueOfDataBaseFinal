import type { Direction } from '@angular/cdk/bidi';
import { tabIndexAfter } from '../../../../../../ui/tabs/tab-index-after';
import { tabMoveForKey } from '../../../../../../ui/tabs/tab-move-for-key';

/**
 * Where a key press moves a viewer showing the `index`th of `count` pictures: the arrows
 * step in the reading direction and wrap around, Home and End reach either end. Null for any
 * other key, and when there is nowhere to go.
 */
export function viewerIndexAfter(
  key: string,
  direction: Direction,
  at: { readonly index: number; readonly count: number },
): number | null {
  const move = tabMoveForKey(key, direction);
  return move === null || at.count < 2 ? null : tabIndexAfter(move, at.index, at.count);
}
