import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';
import { useTheme } from '../../support/theme';

// The identities restyle shared surfaces from global sheets, and only the built stylesheet
// shows whether they still reach them: the build moves each color-mix() declaration into an
// @supports copy emitted after its rule, where a shorthand undoes a later longhand, and a
// component that renames a class drops out of every theme selector without a warning.
const DEFAULT_BASE_URL = 'http://localhost:18080';
const DESKTOP = { width: 1280, height: 800 };

/** The colour a design token resolves to, written the way getComputedStyle writes colours. */
async function tokenColor(page: Page, token: string): Promise<string> {
  return page.evaluate((name) => {
    const probe = document.createElement('span');
    probe.style.color = `var(${name})`;
    document.body.append(probe);
    const color = getComputedStyle(probe).color;
    probe.remove();
    return color;
  }, token);
}

test.describe('identities on the shared surfaces', { tag: '@readonly' }, () => {
  test.use({ viewport: DESKTOP });

  test('Noxus binds every frame with its crimson hoist', async ({ page, context, baseURL }) => {
    await useTheme(context, baseURL ?? DEFAULT_BASE_URL, 'noxus');
    await page.goto('/en/champions');
    await page.waitForLoadState('networkidle');

    const hoist = await page
      .locator('.hextech-frame')
      .first()
      .evaluate((frame) => getComputedStyle(frame).borderInlineStartColor);
    expect(hoist).toBe(await tokenColor(page, '--color-hex-deep'));
  });

  test('Zaun rounds the filter console and rivets its marker', async ({
    page,
    context,
    baseURL,
  }) => {
    await useTheme(context, baseURL ?? DEFAULT_BASE_URL, 'zaun');
    await page.goto('/en/runes');
    await page.waitForLoadState('networkidle');

    const radius = (selector: string) =>
      page
        .locator(selector)
        .first()
        .evaluate((element) => getComputedStyle(element).borderStartStartRadius);
    expect(await radius('lodb-filter-console .console')).not.toBe('0px');
    expect(await radius('lodb-filter-console .marker')).toBe('50%');
  });

  test('sweeps a loading placeholder across twice its width', async ({ page }) => {
    await page.goto('/en/about');
    await page.waitForLoadState('networkidle');

    const size = await page.evaluate(() => {
      const placeholder = document.createElement('span');
      placeholder.className = 'hx-sk';
      document.body.append(placeholder);
      const backgroundSize = getComputedStyle(placeholder).backgroundSize;
      placeholder.remove();
      return backgroundSize;
    });
    expect(size).toBe('200% 100%');
  });
});
