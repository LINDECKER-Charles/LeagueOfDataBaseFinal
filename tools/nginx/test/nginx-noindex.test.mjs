// Boots the built nginx image as staging runs it (LODB_NOINDEX=1, the edge's secret set): the AI
// crawlers get a 403 on both hosts, before anything reaches an upstream, while a search engine
// and a visitor get their answer, each with the noindex. Needs Docker and the image's tag in
// LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { send } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
// What the deploy job generates on a host: 32 random bytes in hexadecimal.
const EDGE_SECRET = '5e'.repeat(32);
const STAGING = { LODB_EDGE_CIDR: '0.0.0.0/0', LODB_EDGE_SECRET: EDGE_SECRET, LODB_NOINDEX: '1' };
const API_HOST = 'api.localhost';
const NOINDEX = 'noindex, nofollow';
// A past patch's sitemap, what Meta's crawler swept on 2026-10-12, and a read of the API host.
const SITEMAP = '/sitemaps/en/13.8.1.xml';
const API_READ = '/v1/champions';
const AI_CRAWLERS = [
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) '
    + 'Chrome/145.0.0.0 Safari/537.36 (compatible; meta-externalagent/1.1 '
    + '(+https://developers.facebook.com/docs/sharing/webmasters/crawler))',
  'Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; GPTBot/1.2; '
    + '+https://openai.com/gptbot)',
  'Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; ClaudeBot/1.0; '
    + '+claudebot@anthropic.com)',
  'CCBot/2.0 (https://commoncrawl.org/faq/)',
];
const SEARCH_ENGINE = 'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)';
const VISITOR = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 '
  + '(KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36';

describe(
  'nginx image on a stack that is never indexed',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' },
  () => {
    let stack;
    const get = (path, options) => send(stack.port, path, { edge: EDGE_SECRET, ...options });

    before(async () => {
      stack = await startStack(IMAGE, STAGING);
    });

    after(() => stack?.stop());

    test('refuses the AI crawlers on both hosts, with the noindex', async () => {
      for (const agent of AI_CRAWLERS) {
        const sitemap = await get(SITEMAP, { agent });
        assert.equal(sitemap.status, 403, agent);
        assert.equal(sitemap.headers['x-robots-tag'], NOINDEX, agent);
        assert.equal((await get(API_READ, { host: API_HOST, agent })).status, 403, agent);
      }
    });

    test('answers a search engine and a visitor, with the noindex', async () => {
      for (const agent of [SEARCH_ENGINE, VISITOR]) {
        const sitemap = await get(SITEMAP, { agent });
        assert.equal(sitemap.status, 200, agent);
        assert.equal(sitemap.headers['x-robots-tag'], NOINDEX, agent);
      }
    });
  },
);
