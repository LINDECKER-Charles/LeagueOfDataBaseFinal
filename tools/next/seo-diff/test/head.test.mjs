import assert from 'node:assert/strict';
import { test } from 'node:test';
import { decodeEntities, parseHead } from '../lib/head.mjs';

const PAGE = `<!doctype html><html lang="fr" dir="ltr"><head>
<title> K&#039;Sante,  champion de LoL — League Of Data Base </title>
<meta data-lodb-seo="" name="description" content="K&#39;Sante &amp; co">
<meta name="robots" content="index, follow">
<link data-lodb-seo rel="canonical" href="http://localhost/fr/champions/KSante">
<link rel="alternate" hreflang="en" href="http://localhost/en/champions/KSante">
<link rel="alternate" hreflang="x-default" href="http://localhost/en/champions/KSante">
<link rel="alternate" type="application/rss+xml" href="/feed.xml">
<script type="application/ld+json">{"@type":"BreadcrumbList"}</script>
<script type="application/ld+json">{broken</script>
<script>var ignored = 1;</script>
</head><body><title>not the head</title></body></html>`;

test('reads the head as crawlers do', () => {
  const head = parseHead(PAGE);

  assert.equal(head.title, "K'Sante, champion de LoL — League Of Data Base");
  assert.equal(head.description, "K'Sante & co");
  assert.equal(head.robots, 'index, follow');
  assert.deepEqual(head.canonicals, ['http://localhost/fr/champions/KSante']);
  assert.deepEqual(head.hreflangs.map((link) => link.lang), ['en', 'x-default']);
  assert.equal(head.htmlLang, 'fr');
  assert.deepEqual(head.jsonLd, [{ '@type': 'BreadcrumbList' }]);
  assert.equal(head.jsonLdErrors.length, 1);
});

test('reports what a head lacks as missing', () => {
  const head = parseHead('<html><head></head><body></body></html>');

  assert.equal(head.title, null);
  assert.equal(head.description, null);
  assert.deepEqual(head.canonicals, []);
  assert.equal(head.htmlLang, null);
});

test('decodes named, decimal and hexadecimal references', () => {
  const decoded = decodeEntities('a &amp; b &#039;c&#x27; &quot;d&quot; &unknown;');

  assert.equal(decoded, `a & b 'c' "d" &unknown;`);
});
