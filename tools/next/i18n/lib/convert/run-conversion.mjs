import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { compileIcu } from '../compile-icu.mjs';
import { flattenKeys } from '../flatten-keys.mjs';
import { toLocaleCode } from '../to-locale-code.mjs';
import { CATALOGUE_DOMAINS } from './catalogue-domains.mjs';
import { convertCatalogue } from './convert-catalogue.mjs';
import { readYamlCatalogues } from './read-yaml-catalogues.mjs';

/**
 * Converts every YAML catalogue of `yamlDir` into Transloco JSON under `catalogueDir`, and
 * returns one summary per written file. Only the files it converts are overwritten: the
 * scopes the features add next to them are never touched. Every message is compiled first,
 * so a catalogue MessageFormat would reject at render time is never written.
 */
export function runConversion({ yamlDir, catalogueDir }) {
  return readYamlCatalogues(yamlDir).map(({ domain, locale, tree }) => {
    const target = CATALOGUE_DOMAINS[domain];
    if (!target) {
      throw new Error(`Unknown translation domain "${domain}" (${locale})`);
    }
    const code = toLocaleCode(locale);
    const converted = convertCatalogue(tree, { locale: locale.replace('_', '-'), ...target });
    assertCompiles(converted.tree, code, domain);
    const file = join(catalogueDir, ...(target.scope ? [target.scope] : []), `${code}.json`);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, `${JSON.stringify(converted.tree, null, 2)}\n`);
    return { domain, locale: code, file, ...converted.stats };
  });
}

function assertCompiles(tree, locale, domain) {
  for (const [key, message] of flattenKeys(tree)) {
    try {
      compileIcu(message, locale);
    } catch (error) {
      throw new Error(`${domain}.${locale} ${key} is not valid ICU: ${error.message}`);
    }
  }
}
