// Checks run inside a loaded gallery page. Each returns a list of failure messages, empty
// when the page is fine, so the caller can gather them all before deciding.

/**
 * Effective rendered width against the device width (docs/architecture/responsive-mobile.md):
 * when content is wider than the screen, a mobile browser widens `innerWidth` instead of
 * scrolling, so `scrollWidth - innerWidth` alone stays at 0 while the page is zoomed out.
 */
export async function overflowFailures(page, deviceWidth) {
  const measure = await page.evaluate((device) => {
    // An element clipped by a scrolling or hidden-overflow ancestor cannot widen the page.
    const unclipped = (el) => {
      for (let node = el.parentElement; node && node !== document.body; node = node.parentElement) {
        if (getComputedStyle(node).overflowX !== 'visible') return false;
      }
      return true;
    };
    return {
      inner: window.innerWidth,
      scroll: document.documentElement.scrollWidth,
      culprits: Array.from(document.querySelectorAll('body *'))
        .filter((el) => el.getBoundingClientRect().right > device + 0.5 && unclipped(el))
        .slice(0, 5)
        .map((el) => `${el.tagName.toLowerCase()}.${String(el.className).split(' ')[0]}`),
    };
  }, deviceWidth);
  const rendered = Math.max(measure.inner, measure.scroll);
  if (rendered <= deviceWidth) {
    return [];
  }
  return [`rendered ${rendered}px on a ${deviceWidth}px screen (${measure.culprits.join(', ')})`];
}

/** The painted identity: attribute, browser chrome colour, and its accent token. */
export async function themeFailures(page, theme) {
  const state = await page.evaluate(() => ({
    attribute: document.documentElement.getAttribute('data-theme'),
    browserColor: document.querySelector('meta[name="theme-color"]')?.getAttribute('content'),
  }));
  const failures = [];
  if (state.attribute !== theme.name) {
    failures.push(`data-theme is ${state.attribute}, expected ${theme.name}`);
  }
  if (state.browserColor !== theme.browserColor) {
    failures.push(`theme-color is ${state.browserColor}, expected ${theme.browserColor}`);
  }
  return failures;
}

/** The accent colour the page resolves, read to prove the four themes paint differently. */
export function accentColour(page) {
  return page.evaluate(() =>
    getComputedStyle(document.documentElement).getPropertyValue('--color-gold').trim(),
  );
}

/** Direction of the page and of the header: the brand sits at the inline start. */
export async function directionFailures(page, expected) {
  const state = await page.evaluate(() => {
    const brand = document.querySelector('.brand-link')?.getBoundingClientRect();
    return {
      dir: document.documentElement.getAttribute('dir'),
      brandCentre: brand ? brand.left + brand.width / 2 : null,
      width: window.innerWidth,
    };
  });
  const failures = [];
  if (state.dir !== expected) {
    failures.push(`<html dir> is ${state.dir}, expected ${expected}`);
  }
  const brandOnRight = state.brandCentre !== null && state.brandCentre > state.width / 2;
  if (brandOnRight !== (expected === 'rtl')) {
    failures.push(`brand is on the ${brandOnRight ? 'right' : 'left'} in a ${expected} page`);
  }
  return failures;
}
