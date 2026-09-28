// Checks that drive the page: the theme picker round trip, and the mirrored keyboard and
// overlay of a right-to-left page. Each returns failure messages, empty when all is well.

const ONE_YEAR_IN_SECONDS = 365 * 24 * 60 * 60;
const COOKIE_SLACK_SECONDS = 120;
const RENDER_TIMEOUT_MS = 2000;

/** Picks Zaun in the dialog: attribute, cookie for a year, focus back on the trigger. */
export async function themePickerFailures(page) {
  await page.locator('.theme-trigger').click();
  const dialog = page.getByRole('dialog');
  await dialog.waitFor();
  const failures = [];
  if ((await dialog.getAttribute('aria-modal')) !== 'true') {
    failures.push('theme dialog is not modal');
  }
  await dialog.locator('.theme-card', { hasText: 'Zaun' }).click();
  await dialog.waitFor({ state: 'detached' });
  failures.push(...(await pickedThemeFailures(page, 'zaun')));
  return failures;
}

async function pickedThemeFailures(page, theme) {
  const failures = [];
  const attribute = await page.evaluate(() => document.documentElement.dataset.theme);
  if (attribute !== theme) {
    failures.push(`picking ${theme} painted ${attribute}`);
  }
  const cookie = (await page.context().cookies()).find((entry) => entry.name === 'lod_theme');
  const lifetime = cookie ? cookie.expires - Date.now() / 1000 : 0;
  if (cookie?.value !== theme || Math.abs(lifetime - ONE_YEAR_IN_SECONDS) > COOKIE_SLACK_SECONDS) {
    failures.push(`lod_theme cookie is ${cookie?.value} for ${Math.round(lifetime)}s`);
  }
  const focused = await page.evaluate(() => document.activeElement?.className ?? '');
  if (!String(focused).includes('theme-trigger')) {
    failures.push(`focus went to "${focused}" instead of the theme trigger`);
  }
  return failures;
}

/** In a right-to-left page the left arrow moves to the next tab, as the tabs read. */
export async function mirroredTabsFailures(page) {
  const tabs = page.getByRole('tablist', { name: 'Champion sheet' }).getByRole('tab');
  await tabs.first().focus();
  await page.keyboard.press('ArrowLeft');
  // Change detection renders after the key event: wait for the selection to land.
  const next = tabs.nth(1).and(page.locator('[aria-selected="true"]:focus'));
  const moved = await next.waitFor({ timeout: RENDER_TIMEOUT_MS }).then(
    () => true,
    () => false,
  );
  return moved ? [] : ['ArrowLeft did not select and focus the next tab in rtl'];
}

/** The CDK overlay follows the page direction, so dialogs mirror with it. */
export async function overlayDirectionFailures(page, expected) {
  await page.getByRole('button', { name: 'Open dialog' }).click();
  const dialog = page.getByRole('dialog', { name: 'Hextech dialog' });
  await dialog.waitFor();
  // The CDK stamps the direction on the host wrapping the pane.
  const dir = await page.locator('.cdk-overlay-container [dir]').first().getAttribute('dir');
  const box = await dialog.boundingBox();
  const width = page.viewportSize()?.width ?? 0;
  await page.keyboard.press('Escape');
  await dialog.waitFor({ state: 'detached' });
  const failures = dir === expected ? [] : [`dialog overlay dir is ${dir}, expected ${expected}`];
  if (!box || box.x < 0 || box.x + box.width > width) {
    failures.push(`dialog does not fit a ${width}px screen`);
  }
  return failures;
}
