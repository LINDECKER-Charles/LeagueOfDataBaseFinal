// Boots the built nginx image as a host runs it (the edge's secret set) and checks the robots
// journal (docker/nginx/njs/robot-journal.js, ADR 0013): a robot's every request and a reader's
// pages reach the API's /internal/robots/journal, once answered, with their address, status and
// time; a reader's files, the probe and a request that came around the edge never do. Needs
// Docker and the image's tag in LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { send } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
const EDGE_SECRET = '7a'.repeat(32);
const HOST = { LODB_EDGE_CIDR: '0.0.0.0/0', LODB_EDGE_SECRET: EDGE_SECRET };
const GOOGLEBOT = 'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)';
const READER = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 '
  + '(KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36';
const GOOGLE_ADDRESS = '66.249.66.1';
const READER_ADDRESS = '203.0.113.20';
// njs posts every 2 s: a few rounds at most.
const WAIT_MS = 15_000;
const POLL_MS = 500;

describe(
  'nginx image reporting to the robots journal',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' },
  () => {
    let stack;
    const get = (path, options) => send(stack.port, path, { edge: EDGE_SECRET, ...options });

    // The records once every expected target is there, or what came within the wait.
    async function journalOf(targets) {
      const deadline = Date.now() + WAIT_MS;
      for (;;) {
        const records = await stack.robotJournal();
        const seen = new Set(records.map((record) => record.u));
        if (targets.every((target) => seen.has(target)) || Date.now() > deadline) {
          return records;
        }
        await new Promise((resolve) => setTimeout(resolve, POLL_MS));
      }
    }

    before(async () => {
      stack = await startStack(IMAGE, HOST);
    });

    after(() => stack?.stop());

    test('reports a robot\'s every request and a reader\'s pages, once answered', async () => {
      await get('/en/12.4.1/items', { agent: GOOGLEBOT, forwardedFor: GOOGLE_ADDRESS });
      await get('/robots.txt', { agent: GOOGLEBOT, forwardedFor: GOOGLE_ADDRESS });
      await get('/build/main-robot.js', { agent: GOOGLEBOT, forwardedFor: GOOGLE_ADDRESS });
      await get('/en/champions?lang=en_US', { agent: READER, forwardedFor: READER_ADDRESS });

      const records = await journalOf([
        '/en/12.4.1/items',
        '/robots.txt',
        '/build/main-robot.js',
        '/en/champions?lang=en_US',
      ]);

      const archive = records.find((record) => record.u === '/en/12.4.1/items');
      assert.ok(archive, JSON.stringify(records));
      assert.equal(archive.a, GOOGLEBOT);
      assert.equal(archive.ip, GOOGLE_ADDRESS);
      assert.equal(archive.m, 'GET');
      assert.equal(archive.s, 200);
      assert.ok(archive.t > 0 && archive.d >= 0 && archive.b > 0, JSON.stringify(archive));
      const reader = records.find((record) => record.u === '/en/champions?lang=en_US');
      assert.equal(reader?.ip, READER_ADDRESS);
      assert.ok(records.some((record) => record.u === '/build/main-robot.js'));
    });

    test('never reports a reader\'s files, the probe, nor what came around the edge', async () => {
      await get('/build/main-reader.js', { agent: READER, forwardedFor: READER_ADDRESS });
      await get('/healthz', { agent: 'Wget' });
      await send(stack.port, '/en/runes', { agent: GOOGLEBOT });
      // A marker the journal must report, after which the others would have come too.
      await get('/en/summoners', { agent: GOOGLEBOT });

      const targets = (await journalOf(['/en/summoners'])).map((record) => record.u);

      assert.ok(targets.includes('/en/summoners'), targets.join(', '));
      for (const left of ['/build/main-reader.js', '/healthz', '/en/runes']) {
        assert.ok(!targets.includes(left), left);
      }
    });
  },
);
