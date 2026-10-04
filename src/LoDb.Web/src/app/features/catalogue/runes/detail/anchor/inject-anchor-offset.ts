import { ViewportScroller } from '@angular/common';
import { DOCUMENT, DestroyRef, inject } from '@angular/core';
import { anchorOffsetOf } from './anchor-offset-of';

/** The router's own offset, which the page puts back as it goes. */
const DEFAULT_OFFSET: [number, number] = [0, 0];

/**
 * While the calling page lives, the router's anchor scrolling stops a fragment's target at its
 * scroll margin (`anchorOffsetOf`), as the legacy page's native scrolling did: a rune card a
 * list links to lands below its row's label instead of under the top edge. The page sets it
 * because it is the one whose fragments the router scrolls to; it is constructed before the
 * router's scroll event, and it restores the router's default when it goes. The server's
 * `ViewportScroller` never scrolls, so nothing is read there.
 */
export function injectAnchorOffset(): void {
  const scroller = inject(ViewportScroller);
  const document = inject(DOCUMENT);
  scroller.setOffset(() => anchorOffsetOf(document));
  inject(DestroyRef).onDestroy(() => scroller.setOffset(DEFAULT_OFFSET));
}
