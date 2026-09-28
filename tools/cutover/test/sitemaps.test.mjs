import assert from 'node:assert/strict';
import { test } from 'node:test';
import { parseSitemap, pathOf, pickSitemaps, sample } from '../lib/sitemaps.mjs';

const INDEX = `<?xml version="1.0" encoding="UTF-8"?>
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <sitemap><loc>https://league-of-data-base.com/sitemaps/latest.xml</loc></sitemap>
  <sitemap><loc>https://league-of-data-base.com/sitemaps/16.18.1.xml</loc></sitemap>
  <sitemap><loc>https://league-of-data-base.com/sitemaps/16.17.1.xml</loc></sitemap>
</sitemapindex>`;

test('reads an index and a list of URLs', () => {
  assert.deepEqual(parseSitemap(INDEX).kind, 'index');
  const urlset = parseSitemap(
    '<urlset xmlns="x"><url><loc>http://localhost:8080/champion/Kai&apos;Sa?a=1&amp;b=2</loc></url></urlset>',
  );
  assert.deepEqual(urlset, { kind: 'urlset', locs: ["http://localhost:8080/champion/Kai'Sa?a=1&b=2"] });
  assert.throws(() => parseSitemap('<html></html>'), /neither/);
});

test('keeps the path and query of a former URL, whatever its host', () => {
  assert.equal(pathOf('https://league-of-data-base.com/objects?lang=fr_FR'), '/objects?lang=fr_FR');
  assert.equal(pathOf('/runes'), '/runes');
});

test('follows the primary sitemap, then the newest version sitemaps', () => {
  const { locs } = parseSitemap(INDEX);
  assert.deepEqual(pickSitemaps(locs, 1).map(pathOf), ['/sitemaps/latest.xml', '/sitemaps/16.18.1.xml']);
  assert.deepEqual(pickSitemaps(locs, 0).map(pathOf), ['/sitemaps/latest.xml']);
});

test('samples evenly, both ends included', () => {
  const list = Array.from({ length: 10 }, (_, index) => index);
  assert.deepEqual(sample(list, 0), list);
  assert.deepEqual(sample(list, 3), [0, 5, 9]);
  assert.deepEqual(sample(list, 1), [0]);
  assert.deepEqual(sample(list, 20), list);
});
