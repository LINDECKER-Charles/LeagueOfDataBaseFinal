import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { describe, it } from 'node:test';
import { flattenKeys } from '../../lib/flatten-keys.mjs';
import { I18N_PATHS } from '../../lib/i18n-paths.mjs';
import { CATALOGUE_DOMAINS } from '../../lib/convert/catalogue-domains.mjs';
import { convertCatalogue } from '../../lib/convert/convert-catalogue.mjs';
import { readYamlCatalogues } from '../../lib/convert/read-yaml-catalogues.mjs';
import { renderLikeSymfony } from '../support/render-like-symfony.mjs';
import { renderLikeTransloco } from '../support/render-like-transloco.mjs';

const PLACEHOLDER = /%([A-Za-z_][A-Za-z0-9_]*)%/g;
const MANY = 7;
// The legacy YAML goes away with the old stack; the parity check goes with it.
const skip = existsSync(I18N_PATHS.yamlDir) ? false : 'no legacy catalogue left';

function paramsOf(...sources) {
  const names = sources.flatMap((source) => [...source.matchAll(PLACEHOLDER)].map((m) => m[1]));
  return Object.fromEntries(names.map((name) => [name, name === 'count' ? MANY : `<${name}>`]));
}

function assertSameRendering(source, converted, locale, key) {
  const params = paramsOf(source);
  const expected = renderLikeSymfony(source, params);
  assert.equal(renderLikeTransloco(converted, params, locale), expected, key);
}

describe('conversion of the real catalogues', { skip }, () => {
  it('renders every message exactly as Symfony did, in all 84 catalogues', () => {
    const catalogues = readYamlCatalogues(I18N_PATHS.yamlDir);
    assert.equal(catalogues.length, 84);
    for (const { domain, locale, tree } of catalogues) {
      const tag = locale.replace('_', '-');
      const raw = flattenKeys(tree);
      const converted = convertCatalogue(tree, { locale: tag, ...CATALOGUE_DOMAINS[domain] });
      for (const [key, message] of flattenKeys(converted.tree)) {
        const singular = raw.get(`${key}_one`);
        const id = `${domain}.${locale} ${key}`;
        if (singular === undefined) {
          assertSameRendering(String(raw.get(key)), message, tag, id);
          continue;
        }
        const params = paramsOf(singular, raw.get(key));
        assert.equal(
          renderLikeTransloco(message, { ...params, count: 1 }, tag),
          renderLikeSymfony(singular, { ...params, count: 1 }),
          id,
        );
        assertSameRendering(raw.get(key), message, tag, id);
      }
    }
  });
});
