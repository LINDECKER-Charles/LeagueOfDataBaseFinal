// Holds the maintenance page (docker/nginx/maintenance) to the site it stands in for: the same
// 21 locales, each with every text; the same colours, faces and shapes as each identity of
// src/LoDb.Web/src/styles; the same @font-face rules, on files the image copies; and the same
// English for a visitor without JavaScript. No Docker needed.
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { describe, test } from 'node:test';
import vm from 'node:vm';

const REPO = new URL('../../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, REPO), 'utf8');
const PAGE = 'docker/nginx/maintenance/';
const ASSETS = `${PAGE}assets/`;
const STYLES = 'src/LoDb.Web/src/styles/';
const FONTS = 'src/LoDb.Web/public/fonts/';
const IDENTITIES = ['zaun', 'noxus', 'spirit-blossom'];
// The page's path to the site's fonts, which the image copies there (docker/nginx/Dockerfile).
const SITE_FONTS = "url('/fonts/";
const PAGE_FONTS = "url('/maintenance/fonts/";

function messages() {
  const context = { window: {} };
  vm.runInNewContext(read(`${ASSETS}messages.js`), context);
  return context.window.lodbMaintenanceMessages;
}

// `--name: value;` declarations of a CSS text, whitespace collapsed; the last one wins.
function declarationsOf(css) {
  const declarations = new Map();
  for (const [, name, value] of css.matchAll(/(--[a-z0-9-]+):\s*([^;]+);/g)) {
    declarations.set(name, value.replace(/\s+/g, ' ').replace(/\( /g, '(').replace(/ \)/g, ')'));
  }
  return declarations;
}

// The bodies of the rules whose selector is exactly `selector`, joined.
function blocksOf(css, selector) {
  const escaped = selector.replace(/[[\]]/g, '\\$&');
  return [...css.matchAll(new RegExp(`(?:^|\\n)${escaped} \\{([^}]*)\\}`, 'g'))]
    .map((match) => match[1])
    .join('\n');
}

function themeSource(identity) {
  const files = ['tokens.css', 'type.css'].map((name) =>
    read(`${STYLES}theme/${identity}/${name}`),
  );
  return declarationsOf(files.map((css) => blocksOf(css, `[data-theme='${identity}']`)).join(''));
}

const fontFacesOf = (css) => css.match(/@font-face \{[^}]*\}/g) ?? [];
const tokens = read(`${ASSETS}tokens.css`);
const pageTokens = declarationsOf(blocksOf(tokens, ':root'));

describe('maintenance page against the site', () => {
  test('speaks the 21 site locales, each with every text of English', () => {
    const locales = [
      ...read('src/LoDb.Web/src/app/core/api/generated/models/ui-locale-array.ts').matchAll(
        /'([a-z-]+)'/g,
      ),
    ].map((match) => match[1]);
    const texts = messages();
    assert.equal(locales.length, 21);
    assert.deepEqual(Object.keys(texts).sort(), [...locales].sort());
    const keys = Object.keys(texts.en).sort();
    for (const locale of locales) {
      assert.deepEqual(Object.keys(texts[locale]).sort(), keys, locale);
      for (const key of keys) {
        const text = texts[locale][key];
        assert.ok(typeof text === 'string' && text.trim() === text && text !== '', locale + key);
      }
    }
  });

  test("paints Hextech with the site's default tokens", () => {
    const site = declarationsOf(read(`${STYLES}foundation/tokens.css`));
    for (const [name, value] of pageTokens) {
      assert.equal(value, site.get(name), name);
    }
  });

  test("paints every other identity with its own tokens, none of them left to Hextech's", () => {
    for (const identity of IDENTITIES) {
      const site = themeSource(identity);
      const page = declarationsOf(blocksOf(tokens, `[data-theme='${identity}']`));
      for (const [name, value] of page) {
        assert.equal(value, site.get(name), `${identity} ${name}`);
      }
      for (const name of pageTokens.keys()) {
        assert.ok(!site.has(name) || page.has(name), `${identity} restates ${name}`);
      }
    }
  });

  test('reads only the tokens it declares', () => {
    const declared = declarationsOf(tokens);
    for (const [, name] of read(`${ASSETS}page.css`).matchAll(/var\((--[a-z0-9-]+)\)/g)) {
      assert.ok(declared.has(name), name);
    }
  });

  test("declares the site's faces as the site does, on files the image copies", () => {
    const sources = ['foundation/fonts.css', ...IDENTITIES.map((name) => `theme/${name}/type.css`)];
    const site = sources
      .flatMap((path) => fontFacesOf(read(`${STYLES}${path}`)))
      .map((face) => face.replaceAll(SITE_FONTS, PAGE_FONTS));
    const page = fontFacesOf(read(`${ASSETS}fonts.css`));
    assert.deepEqual(page, site);
    for (const [, file] of page.join('\n').matchAll(/url\('\/maintenance\/fonts\/([^']+)'\)/g)) {
      assert.ok(existsSync(new URL(`${FONTS}${file}`, REPO)), file);
    }
  });

  test('reads the English of its messages to a visitor without JavaScript', () => {
    const html = read(`${PAGE}index.html`);
    const english = messages().en;
    for (const [, key, text] of html.matchAll(/data-message="([a-zA-Z]+)">([^<]+)</g)) {
      assert.equal(text.replace(/\s+/g, ' ').trim(), english[key], key);
    }
    assert.match(html, new RegExp(`<title>${english.documentTitle}</title>`));
  });

  test('links only files the image serves under /maintenance/', () => {
    const html = read(`${PAGE}index.html`);
    const linked = [...html.matchAll(/(?:href|src)="\/maintenance\/([^"]+)"/g)].map((m) => m[1]);
    assert.ok(linked.length >= 5, linked.join());
    const copied = (name) =>
      name === 'favicon-96x96.png'
        ? existsSync(new URL('src/LoDb.Web/public/favicon/favicon-96x96.png', REPO))
        : existsSync(new URL(`${ASSETS}${name}`, REPO));
    for (const name of linked) {
      assert.ok(copied(name), name);
    }
    assert.doesNotMatch(html, /<script>|<style|style="/);
  });
});
