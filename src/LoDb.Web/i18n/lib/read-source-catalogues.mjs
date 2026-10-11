import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const JSON_FILE = /^(.+)\.json$/;

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
      catalogues.set(locale, JSON.parse(readFileSync(file, 'utf8')));
    }
  }
  return catalogues;
}

/**
 * Reads the authored catalogues (`public/i18n`): the root files name the locales, each
 * sub-folder is a scope, the root scope is named ''. A scope without a file for a locale has
 * no entry for it.
 */
export function readSourceCatalogues(dir) {
  const locales = localesIn(dir);
  const scopeNames = readdirSync(dir, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
  const scopes = new Map();
  for (const scope of ['', ...scopeNames]) {
    scopes.set(scope, readScope(join(dir, scope), locales));
  }
  return { locales, scopes };
}
