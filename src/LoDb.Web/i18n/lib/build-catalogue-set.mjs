import { completeCatalogue } from './complete-catalogue.mjs';
import { pickKeys } from './pick-keys.mjs';

// The runtime's fallback language (DEFAULT_LOCALE in src/app/core/i18n): every catalogue is
// completed with its messages.
const REFERENCE_LOCALE = 'en';

function filePath(scope, locale) {
  return scope === '' ? `${locale}.json` : `${scope}/${locale}.json`;
}

function builtScope(scope, catalogues, locales, errors) {
  const reference = catalogues.get(REFERENCE_LOCALE);
  if (reference === undefined) {
    errors.push(`${scope || '(root)'}: no ${REFERENCE_LOCALE} catalogue`);
    return new Map();
  }
  const built = new Map();
  for (const locale of locales) {
    built.set(locale, completeCatalogue(reference, catalogues.get(locale) ?? {}));
  }
  return built;
}

// The chrome (header, footer) shows a few labels of other scopes on every page: the root
// catalogue carries them, so that no page loads those whole scopes for them.
function lendChromeKeys(built, chromeKeys, errors) {
  for (const [locale, root] of built.get('')) {
    for (const [scope, paths] of Object.entries(chromeKeys)) {
      if (Object.hasOwn(root, scope)) {
        errors.push(`${filePath('', locale)}: the root catalogue already has a "${scope}" key`);
        continue;
      }
      try {
        root[scope] = pickKeys(built.get(scope)?.get(locale) ?? {}, paths);
      } catch (error) {
        errors.push(`${filePath(scope, locale)}: ${error.message}`);
      }
    }
  }
}

/**
 * The catalogues the site ships, from the authored ones (`readSourceCatalogues`): every scope
 * in every locale, completed with `en`, the root carrying the chrome's borrowed labels
 * (`chromeKeys`: scope → dotted keys). `files` maps a path relative to the
 * `i18n/` folder of the build to its catalogue; `errors` lists what failed, empty on success.
 */
export function buildCatalogueSet({ locales, scopes }, chromeKeys) {
  const errors = [];
  const built = new Map();
  for (const [scope, catalogues] of scopes) {
    built.set(scope, builtScope(scope, catalogues, locales, errors));
  }
  lendChromeKeys(built, chromeKeys, errors);
  const files = new Map();
  for (const [scope, catalogues] of built) {
    for (const [locale, catalogue] of catalogues) {
      files.set(filePath(scope, locale), catalogue);
    }
  }
  return { files, errors };
}
