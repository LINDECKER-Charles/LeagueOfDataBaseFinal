import { execInStack } from '../../support/stack';
import { expect, test } from '../../support/test';

// The folder the successive builds of web-ssr share (compose.yaml, volume front-builds).
const FRONT_BUILDS = '/srv/front-builds';
const IMMUTABLE = 'public, max-age=31536000, immutable';
const HASH_LENGTH = 8;
const BASE36 = 36;
const NOT_BUNDLES = [
  '/build/',
  '/build/en/about',
  '/build/offline.html',
  '/build/main-00000000.js',
];

// A bundle name of another build (src/server/hashed-asset.ts), new on each run.
function previousBundleName(): string {
  const hash = Math.random().toString(BASE36).slice(2).padEnd(HASH_LENGTH, '0');
  return `chunk-${hash.slice(0, HASH_LENGTH).toUpperCase()}.js`;
}

// Deploying twice in a test is out of reach: the previous web-ssr's part is played by hand. At
// its start it published its bundles into the shared folder, which the current image lacks.
test.describe('bundles of a previous build', () => {
  test('still load in a tab opened before the deployment', async ({ page, consoleErrors }) => {
    const name = previousBundleName();
    const bundle = `export const build = 'previous';\n`;
    execInStack('web-ssr', ['sh', '-c', `cat > ${FRONT_BUILDS}/${name}`], bundle);
    try {
      await page.goto('/en/about');

      // The lazy import a route of the old tab makes, from the page's own origin.
      const build: unknown = await page.evaluate(
        `import('/build/${name}').then((module) => module.build)`,
      );
      const again = await page.request.get(`/build/${name}`);

      expect(build).toBe('previous');
      expect(again.headers()['cache-control']).toBe(IMMUTABLE);
      expect(again.headers()['x-cache-status']).toBe('HIT');
      expect(consoleErrors).toEqual([]);
    } finally {
      execInStack('web-ssr', ['rm', '-f', `${FRONT_BUILDS}/${name}`]);
    }

    await test.step('cache the hashed bundles of /build/ for a year', async () => {
      const html = await (await page.request.get('/en/about')).text();
      const bundle = /<script src="(\/build\/main-[\w-]+\.js)"/.exec(html)?.[1];

      expect(bundle).toBeDefined();
      const response = await page.request.get(bundle ?? '');
      expect(response.status()).toBe(200);
      expect(response.headers()['cache-control']).toBe(IMMUTABLE);
    });

    // A name no build has is a plain 404 of the SSR server, never a page it renders.
    await test.step('route no page under /build/', async () => {
      for (const path of NOT_BUNDLES) {
        const response = await page.request.get(path);

        expect(response.status(), path).toBe(404);
        expect(response.headers()['content-type'], path).toContain('text/plain');
      }
    });
  });
});
