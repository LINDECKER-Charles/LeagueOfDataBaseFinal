import type { TabMove } from './tab-move';

/** Index of the tab a move lands on; the arrows wrap around at both ends of the list. */
export function tabIndexAfter(move: TabMove, index: number, count: number): number {
  switch (move) {
    case 'first':
      return 0;
    case 'last':
      return count - 1;
    case 'next':
      return (index + 1) % count;
    case 'previous':
      return (index - 1 + count) % count;
  }
}
