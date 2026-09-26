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

/**
 * Measures the page against the device width. On a phone (`isMobile`) the browser widens the
 * layout viewport instead of scrolling, so the effective width is the larger of innerWidth
 * and scrollWidth, compared with the width of the device. When the page is too wide, the
 * culprits are the elements that stick out of their parent, in either direction (a right to
 * left page overflows on the left). Left out: hidden elements, fixed ones (they follow the
 * viewport) and the content of a clipping or scrolling ancestor, which cannot widen the page.
 */
export async function overflowOf(page: Page, deviceWidth: number): Promise<Overflow> {
  return page.evaluate(
    ([limit, max]) => {
      const width = Math.max(window.innerWidth, document.documentElement.scrollWidth);
      if (width <= limit) return { width, offenders: [] };
      const clipped = (element: Element): boolean => {
        for (let up = element.parentElement; up !== null; up = up.parentElement) {
          if (getComputedStyle(up).overflowX !== 'visible') return true;
        }
        return false;
      };
      // The parent that lays the element out: a `display: contents` host has no box.
      const container = (element: Element): Element | null => {
        let up = element.parentElement;
        while (up !== null && getComputedStyle(up).display === 'contents') up = up.parentElement;
        return up;
      };
      const excess = (element: Element): number => {
        const parent = container(element);
        const box = element.getBoundingClientRect();
        if (parent === null || box.width === 0) return 0;
        const outer = parent.getBoundingClientRect();
        return Math.max(box.right - outer.right, outer.left - box.left);
      };
      const named = (element: Element): string => {
        const box = element.getBoundingClientRect();
        const classes = [...element.classList].slice(0, 3).join('.');
        const name = classes === '' ? element.localName : `${element.localName}.${classes}`;
        return `${name} [${Math.round(box.left)}, ${Math.round(box.right)}]`;
      };
      const offenders = [...document.body.querySelectorAll('*')]
        // Rendered only: the panel of a closed <details> keeps a box it never paints.
        .filter((element) => element.checkVisibility())
        .filter((element) => getComputedStyle(element).position !== 'fixed')
        .map((element) => ({ element, excess: excess(element) }))
        .filter((entry) => entry.excess > 0.5 && !clipped(entry.element))
        .sort((a, b) => b.excess - a.excess)
        .slice(0, max)
        .map((entry) => named(entry.element));
      return { width, offenders };
    },
    [deviceWidth, MAX_OFFENDERS] as const,
  );
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
