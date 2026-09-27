/** Where the router scrolls a fragment's target without a margin to keep: flush at the top. */
const NO_OFFSET: [number, number] = [0, 0];

/** The element the page's fragment names, or null (no fragment, or one naming nothing). */
function fragmentTargetOf(document: Document): Element | null {
  const fragment = document.location.hash.slice(1);
  if (fragment === '') {
    return null;
  }
  try {
    return document.getElementById(decodeURIComponent(fragment));
  } catch {
    // A malformed escape names no element; the scroll must not fail for it.
    return null;
  }
}

/**
 * The offset the router's anchor scrolling keeps above the target of the page's fragment:
 * the target's own `scroll-margin-block-start`, as native fragment scrolling honours it (the
 * 5.5rem every `[id]` gets in foundation/base.css). Angular's `ViewportScroller` scrolls to
 * the element's box and ignores that margin, which lands the target flush with the top.
 */
export function anchorOffsetOf(document: Document): [number, number] {
  const target = fragmentTargetOf(document);
  const view = document.defaultView;
  if (target === null || view === null) {
    return NO_OFFSET;
  }
  const margin = Number.parseFloat(view.getComputedStyle(target).scrollMarginBlockStart);
  return [0, Number.isFinite(margin) ? margin : 0];
}
