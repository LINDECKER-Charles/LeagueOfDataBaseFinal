import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// Heritage H1 (a clip kept playing once its island was gone) and H3 (`muted` never applied):
// every clip starts muted, and none plays on once the reader leaves it, whatever the way
// out. The pager's way out is specs/champions/champions.spec.ts's.
const CHAMPION = '/en/champions/Annie';
const CLIP = '#abilities video';
const LOG = 'lodbClips';
const KEEP = 'lodbKeptClip';
const DESKTOP = { width: 1280, height: 800 };
const CLIP_START_MS = 15_000;
// HTMLMediaElement.NETWORK_NO_SOURCE: no source could be loaded.
const NO_SOURCE = 3;

interface ClipEvent {
  readonly event: 'inserted' | 'play';
  readonly muted: boolean;
}

// Logs, before any script of the page, the muted state of each video as it enters the
// document and as it starts playing.
async function logClips(page: Page): Promise<void> {
  await page.addInitScript((key) => {
    const log: { event: string; muted: boolean }[] = [];
    Reflect.set(window, key, log);
    document.addEventListener(
      'play',
      (event) => {
        if (event.target instanceof HTMLVideoElement) {
          log.push({ event: 'play', muted: event.target.muted });
        }
      },
      true,
    );
    new MutationObserver((records) => {
      for (const node of records.flatMap((record) => [...record.addedNodes])) {
        const videos = node instanceof Element ? [...node.querySelectorAll('video')] : [];
        if (node instanceof HTMLVideoElement) videos.push(node);
        for (const video of videos) log.push({ event: 'inserted', muted: video.muted });
      }
    }).observe(document, { childList: true, subtree: true });
  }, LOG);
}

async function clipLog(page: Page): Promise<ClipEvent[]> {
  return page.evaluate((key) => Reflect.get(window, key) as ClipEvent[], LOG);
}

// Waits for the clip to play; skips when its media host cannot be reached from here.
async function waitForPlayback(page: Page): Promise<void> {
  const clip = page.locator(CLIP);
  await clip.scrollIntoViewIfNeeded();
  const playing = () => clip.evaluate((video: HTMLVideoElement) => !video.paused);
  try {
    await expect.poll(playing, { timeout: CLIP_START_MS }).toBe(true);
  } catch (error) {
    const unreachable = await clip.evaluate(
      (video: HTMLVideoElement, none) => video.error !== null || video.networkState === none,
      NO_SOURCE,
    );
    test.skip(unreachable, 'the media host of the clips is unreachable from this browser');
    throw error;
  }
}

async function keepClip(page: Page): Promise<void> {
  await page.locator(CLIP).evaluate((video, key) => Reflect.set(window, key, video), KEEP);
}

// The kept clip, whatever became of it: paused, and out of the document.
async function keptClip(page: Page): Promise<{ paused: boolean; connected: boolean }> {
  return page.evaluate((key) => {
    const video = Reflect.get(window, key) as HTMLVideoElement;
    return { paused: video.paused, connected: video.isConnected };
  }, KEEP);
}

async function openChampion(page: Page): Promise<void> {
  await logClips(page);
  await page.goto(CHAMPION);
  await page.waitForLoadState('networkidle');
}

test.describe('clips of the abilities', () => {
  test.use({ viewport: DESKTOP });

  test('start muted, each of them, from the moment they exist', async ({ page }) => {
    await openChampion(page);
    const tabs = page.locator('#abilities').getByRole('tab');
    const count = await tabs.count();

    for (let index = 0; index < count; index += 1) {
      await tabs.nth(index).click();
      await expect(tabs.nth(index)).toHaveAttribute('aria-selected', 'true');
      await expect(page.locator(CLIP)).toHaveCount(1);
      const muted = await page.locator(CLIP).evaluate((video: HTMLVideoElement) => video.muted);
      expect(muted).toBe(true);
    }

    const log = await clipLog(page);
    expect(log.filter((entry) => entry.event === 'inserted').length).toBeGreaterThanOrEqual(count);
    expect(log.filter((entry) => !entry.muted)).toEqual([]);
  });

  test('leave no clip playing behind when the reader switches ability', async ({ page }) => {
    await openChampion(page);
    await waitForPlayback(page);
    await keepClip(page);

    await page.locator('#abilities').getByRole('tab').nth(1).click();

    // The clip is paused as its view is destroyed, right after the click.
    await expect.poll(() => keptClip(page)).toEqual({ paused: true, connected: false });
    const playing = await page.locator('video').evaluateAll(
      (videos) => videos.filter((video) => !(video as HTMLVideoElement).paused).length,
    );
    expect(playing).toBeLessThanOrEqual(1);
  });

  test('stop when the reader leaves by the header menu', async ({ page }) => {
    await openChampion(page);
    await waitForPlayback(page);
    await keepClip(page);

    await page.locator('header details.switcher--nav summary').click();
    await page.locator('header details.switcher--nav a[href="/en/items"]').click();

    await expect.poll(() => new URL(page.url()).pathname).toBe('/en/items');
    await expect.poll(() => keptClip(page)).toEqual({ paused: true, connected: false });
  });

  test('stop when the reader goes back in the history', async ({ page }) => {
    await logClips(page);
    await page.goto('/en/champions');
    await page.waitForLoadState('networkidle');
    await page.locator(`main a[href="${CHAMPION}"]`).first().click();
    await expect.poll(() => new URL(page.url()).pathname).toBe(CHAMPION);
    await waitForPlayback(page);
    await keepClip(page);

    await page.goBack();

    await expect.poll(() => new URL(page.url()).pathname).toBe('/en/champions');
    await expect.poll(() => keptClip(page)).toEqual({ paused: true, connected: false });
  });

  test('give way to the poster for a reader who asked for reduced motion', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await openChampion(page);

    await expect(page.locator(CLIP)).toHaveCount(0);
    await expect(page.locator('#abilities img.poster')).toBeVisible();
    expect(await clipLog(page)).toEqual([]);
  });
});
