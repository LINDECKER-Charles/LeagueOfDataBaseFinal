import type { Page } from '@playwright/test';

/** The narrowest phone the site supports (heritage H6): 320 CSS pixels. */
export const NARROW_PHONE = { width: 320, height: 640 };
/** Below this font size, iOS zooms into a field on focus (heritage H6). */
export const MIN_FIELD_FONT_PX = 16;
// Elements listed when a page overflows: enough to name the culprits.
const MAX_OFFENDERS = 5;

/** How wide a page lays out, and the elements that widen it when it is too wide. */
export interface Overflow {
  /** The effective width: a phone widens its layout viewport to fit the content. */
  readonly width: number;
  /** The elements that stick out of their parent's box, as `tag.class [left, right]`. */
  readonly offenders: readonly string[];
}

/** An element that sticks out of the box laying it out, and by how many pixels. */
interface Sticking {
  readonly name: string;
  readonly left: number;
  readonly right: number;
  readonly excess: number;
}

// Runs in the page, self-contained: each element that sticks out of the box laying it out (a
// `display: contents` host has none), in either direction, a right to left page overflowing
// on the left. Left out: hidden elements, fixed ones (they follow the viewport) and the content
// of a clipping or scrolling ancestor, which cannot widen the page.
function stickingOut(): Sticking[] {
  const style = (element: Element) => getComputedStyle(element);
  const container = (element: Element): Element | null => {
    let up = element.parentElement;
    while (up !== null && style(up).display === 'contents') up = up.parentElement;
    return up;
  };
  const clipped = (element: Element): boolean => {
    for (let up = element.parentElement; up !== null; up = up.parentElement) {
      if (style(up).overflowX !== 'visible') return true;
    }
    return false;
  };
  return [...document.body.querySelectorAll('*')].flatMap((element) => {
    const parent = container(element);
    const box = element.getBoundingClientRect();
    const outer = parent?.getBoundingClientRect();
    const excess =
      outer === undefined ? 0 : Math.max(box.right - outer.right, outer.left - box.left);
    // Rendered only: the panel of a closed <details> keeps a box it never paints.
    if (box.width === 0 || excess <= 0.5 || !element.checkVisibility()) return [];
    if (style(element).position === 'fixed' || clipped(element)) return [];
    const classes = [...element.classList].slice(0, 3).join('.');
    const name = classes === '' ? element.localName : `${element.localName}.${classes}`;
    return [{ name, left: box.left, right: box.right, excess }];
  });
}

/**
 * Measures the page against the device width. On a phone (`isMobile`) the browser widens the
 * layout viewport instead of scrolling, so the effective width is the larger of innerWidth
 * and scrollWidth, compared with the width of the device. A page too wide names the elements
 * that stick out the most.
 */
export async function overflowOf(page: Page, deviceWidth: number): Promise<Overflow> {
  const width = await page.evaluate(() =>
    Math.max(window.innerWidth, document.documentElement.scrollWidth),
  );
  if (width <= deviceWidth) return { width, offenders: [] };
  const offenders = (await page.evaluate(stickingOut))
    .sort((a, b) => b.excess - a.excess)
    .slice(0, MAX_OFFENDERS)
    .map((entry) => `${entry.name} [${Math.round(entry.left)}, ${Math.round(entry.right)}]`);
  return { width, offenders };
}

/** The form fields whose font would make iOS zoom, as `tag[name]: size`. */
export async function smallFields(page: Page): Promise<string[]> {
  return page.evaluate((minimum) => {
    const selector =
      'input:not([type=hidden]):not([type=checkbox]):not([type=radio]):not([type=range])' +
      ':not([type=button]):not([type=submit]):not([type=reset]):not([type=color]),' +
      'select, textarea';
    return [...document.querySelectorAll(selector)].flatMap((field) => {
      const size = Number.parseFloat(getComputedStyle(field).fontSize);
      const name = field.getAttribute('name') ?? field.getAttribute('aria-label') ?? field.id;
      return size < minimum ? [`${field.localName}[${name}]: ${size}px`] : [];
    });
  }, MIN_FIELD_FONT_PX);
}
