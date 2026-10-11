// `npm run i18n:build`, run before every build of the workspace (`prebuild:*`, `prestart`):
// turns the authored catalogues (public/i18n) into the ones the site ships (dist/i18n, copied
// to `i18n/` of each build by angular.json). Each locale's catalogue is completed with `en`,
// its ICU messages compiled for PrecompiledTranspiler (src/app/core/i18n/messages), and the
// root carries the few labels the chrome borrows from other scopes (chrome-keys.json).
// Fails, listing every problem: a message that does not compile, a scope without `en`, a
// borrowed label that is missing.
// Tests: `node --test "i18n/**/*.test.mjs"` (src/LoDb.Web).
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { buildCatalogueSet } from './lib/build-catalogue-set.mjs';
import { readSourceCatalogues } from './lib/read-source-catalogues.mjs';
import { writeCatalogues } from './lib/write-catalogues.mjs';

const workspace = join(import.meta.dirname, '..');
const sourceDir = join(workspace, 'public', 'i18n');
const outputDir = join(workspace, 'dist', 'i18n');
const chromeKeys = JSON.parse(readFileSync(join(import.meta.dirname, 'chrome-keys.json'), 'utf8'));

const { files, errors } = buildCatalogueSet(readSourceCatalogues(sourceDir), chromeKeys);
if (errors.length > 0) {
  console.error(`i18n:build: ${errors.length} problems:\n${errors.join('\n')}`);
  process.exit(1);
}
writeCatalogues(outputDir, files);
console.log(`i18n:build: ${files.size} catalogues written to dist/i18n.`);
