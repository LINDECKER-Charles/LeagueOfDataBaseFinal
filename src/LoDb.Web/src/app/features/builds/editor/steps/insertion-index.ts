/** The indexes a CDK drop reports, and whether it stayed in its list. */
interface DropPlace {
  readonly previousIndex: number;
  readonly currentIndex: number;
  readonly sameList: boolean;
}

/**
 * The insertion index of a CDK drop, from 0 to the length, which the purchase order's moves
 * take. The CDK reports where the entry rests once moved: within its list, a move down rests
 * one place before the gap it was dropped into; in another list, both read alike.
 */
export function insertionIndex(drop: DropPlace): number {
  const movedDown = drop.sameList && drop.currentIndex > drop.previousIndex;
  return movedDown ? drop.currentIndex + 1 : drop.currentIndex;
}
