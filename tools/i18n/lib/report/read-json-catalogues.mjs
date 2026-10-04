import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { flattenKeys } from '../flatten-keys.mjs';

const JSON_FILE = /^(.+)\.json$/;

/**
 * Reads the Transloco catalogues: the root files give the locales, each sub-folder is a
 * scope (`seo`, `about`, `api`, then one per feature). The root scope is named ''. A scope
 * without a file for some locale simply has no entry for it.
 */
export function readJsonCatalogues(catalogueDir) {
  const locales = localesIn(catalogueDir);
  const scopeNames = readdirSync(catalogueDir, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
  const scopes = new Map();
  for (const scope of ['', ...scopeNames]) {
    scopes.set(scope, readScope(join(catalogueDir, scope), locales));
  }
  return { locales, scopes };
}

function localesIn(dir) {
  return readdirSync(dir)
    .map((file) => JSON_FILE.exec(file)?.[1])
    .filter((locale) => locale !== undefined)
    .sort();
}

function readScope(dir, locales) {
  const catalogues = new Map();
  for (const locale of locales) {
    const file = join(dir, `${locale}.json`);
    if (existsSync(file)) {
      catalogues.set(locale, flattenKeys(JSON.parse(readFileSync(file, 'utf8'))));
    }
  }
  return catalogues;
}
