// Bundles the service worker into dist/web/browser/sw.js, after `build:web` (npm's
// `postbuild:web`). The application builder has no second browser entry, so the worker is
// bundled here with the esbuild that @angular/build itself runs on. Web build only: the
// shell build gets no worker.
import { build } from 'esbuild';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';

const workspace = join(import.meta.dirname, '..', '..');
const offlinePage = await readFile(join(workspace, 'public', 'offline.html'));
// A new offline page changes the worker's bytes, which is what makes browsers install it.
const offlineRevision = createHash('sha256').update(offlinePage).digest('hex').slice(0, 12);

await build({
  absWorkingDir: workspace,
  entryPoints: ['src/sw/worker.ts'],
  outfile: 'dist/web/browser/sw.js',
  bundle: true,
  format: 'iife',
  platform: 'browser',
  target: 'es2022',
  minify: true,
  legalComments: 'none',
  define: { LODB_OFFLINE_REVISION: JSON.stringify(offlineRevision) },
  logLevel: 'warning',
});
