/** Where a strip of tabs is scrolled, as its element measures it. */
export interface TabScroll {
  /** `scrollLeft`: negative toward the end of a right-to-left strip. */
  readonly offset: number;
  readonly scrollWidth: number;
  readonly clientWidth: number;
}

// The width an edge fades over: enough for a cut label to read as cut.
const FADE = '2rem';
// Sub-pixel widths: a strip that fits may still measure one pixel wider.
const SLACK_PX = 1;

/**
 * The mask that fades the edges of a strip of tabs past which tabs are hidden, so that a tab
 * out of view (« Historique » on a phone) reads as one to scroll to; null while all fit. Its
 * gradient runs along the reading direction: the start fades once scrolled away from it.
 */
export function tabFade(scroll: TabScroll, isRtl: boolean): string | null {
  const travelled = Math.abs(scroll.offset);
  const hasStart = travelled > SLACK_PX;
  const hasEnd = travelled + scroll.clientWidth < scroll.scrollWidth - SLACK_PX;
  if (!hasStart && !hasEnd) {
    return null;
  }
  const side = isRtl ? 'to left' : 'to right';
  const start = hasStart ? FADE : '0px';
  const end = hasEnd ? FADE : '0px';
  return `linear-gradient(${side}, transparent, #000 ${start}, #000 calc(100% - ${end}), transparent)`;
}
